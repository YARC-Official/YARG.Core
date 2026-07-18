using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;
using StemInfo = YARG.Core.Audio.StemMixer.StemInfo;

namespace YARG.Core.Song
{
    /// <summary>
    /// Builds a StemMixer directly from a raw, multi-channel .mogg stream plus a
    /// channel-index/panning map. Originally lived inline in RBCONEntry.LoadAudio;
    /// factored out so ini/sng entries that ship a .mogg (instead of split per-stem
    /// files) can use the exact same path.
    /// </summary>
    internal static class MoggAudioLoader
    {
        public const int UNENCRYPTED_MOGG = 0xA;
        private const int YARG_MOGG = 0xF0;

        public static bool IsSupportedVersion(int version) => version is UNENCRYPTED_MOGG or YARG_MOGG;

        /// <summary>
        /// Reads the mogg header, builds the mixer, and wires up one channel group
        /// per stem present in <paramref name="indices"/>. Disposes <paramref name="stream"/>
        /// on any failure path; on success the mixer takes ownership of it.
        /// </summary>
        public static StemMixer? BuildMixer(Stream stream, string mixerName, float speed, double volume,
            bool clampStemVolume, in RBAudio<int> indices, in RBAudio<float> panning, params SongStem[] ignoreStems)
        {
            int version = stream.Read<int>(Endianness.Little);
            if (!IsSupportedVersion(version))
            {
                YargLogger.LogError("Unsupported or encrypted .mogg!");
                stream.Dispose();
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            var mixer = GlobalAudioHandler.CreateMixer(mixerName, speed, volume, clampStemVolume: clampStemVolume,
                normalize: true);
            if (mixer == null)
            {
                YargLogger.LogError("Mogg failed to load!");
                stream.Dispose();
                return null;
            }

            var stemInfos = new List<StemInfo>();

            if (indices.Drums.Length > 0 && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 1:
                    case 2:
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums, panning.Drums));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[1..3], panning.Drums[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[1..2], panning.Drums[2..4]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[2..4], panning.Drums[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[1..3], panning.Drums[2..6]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[3..5], panning.Drums[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[0..2], panning.Drums[0..4]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[2..4], panning.Drums[4..8]));
                        stemInfos.Add(new StemInfo(SongStem.Drums, indices.Drums[4..6], panning.Drums[8..12]));
                        break;
                }
            }

            if (indices.Bass.Length > 0 && !ignoreStems.Contains(SongStem.Bass))
                stemInfos.Add(new StemInfo(SongStem.Bass, indices.Bass, panning.Bass));

            if (indices.Guitar.Length > 0 && !ignoreStems.Contains(SongStem.Guitar))
                stemInfos.Add(new StemInfo(SongStem.Guitar, indices.Guitar, panning.Guitar));

            if (indices.Keys.Length > 0 && !ignoreStems.Contains(SongStem.Keys))
                stemInfos.Add(new StemInfo(SongStem.Keys, indices.Keys, panning.Keys));

            if (indices.Vocals.Length > 0 && !ignoreStems.Contains(SongStem.Vocals))
                stemInfos.Add(new StemInfo(SongStem.Vocals, indices.Vocals, panning.Vocals));

            if (indices.Track.Length > 0 && !ignoreStems.Contains(SongStem.Song))
                stemInfos.Add(new StemInfo(SongStem.Song, indices.Track, panning.Track));

            if (indices.Crowd.Length > 0 && !ignoreStems.Contains(SongStem.Crowd))
                stemInfos.Add(new StemInfo(SongStem.Crowd, indices.Crowd, panning.Crowd));

            mixer.AddChannels(stream, stemInfos.ToArray());

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Failed to add any stems!");
                stream.Dispose();
                mixer.Dispose();
                return null;
            }

            YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
            return mixer;
        }

        /// <summary>
        /// Parses a single-song .dta fragment (just the `song` block - tracks/pan/vol)
        /// into channel indices and computed L/R pan gains, using the same math RBCON
        /// uses. Intended for a small sidecar file that rides next to an ini song's
        /// raw .mogg, e.g. "song.mogg.dta".
        /// </summary>
        public static bool TryParseChannelMap(FixedArray<byte> dtaBytes, out RBAudio<int> indices, out RBAudio<float> panning)
        {
            indices = RBAudio<int>.Empty;
            panning = RBAudio<float>.Empty;
            try
            {
                var container = YARGDTAReader.Create(dtaBytes);
                if (!YARGDTAReader.StartNode(ref container))
                {
                    return false;
                }

                string name = YARGDTAReader.GetNameOfNode(ref container, false);
                var dta = DTAEntry.Create(name, container);
                YARGDTAReader.EndNode(ref container);

                if (dta.Indices == null || dta.Pans == null || dta.Volumes == null)
                {
                    YargLogger.LogError("Mogg channel-map sidecar is missing tracks/pan/vol!");
                    return false;
                }

                indices = dta.Indices.Value;
                var pans = dta.Pans;
                var volumes = dta.Volumes;

                float[] CalculateStemValues(int[] idx)
                {
                    var values = new float[2 * idx.Length];
                    for (int i = 0; i < idx.Length; i++)
                    {
                        float theta = (pans[idx[i]] + 1) * (MathF.PI / 4);
                        float volRatio = MathF.Pow(10, volumes[idx[i]] / 20);
                        values[2 * i] = volRatio * MathF.Cos(theta);
                        values[2 * i + 1] = volRatio * MathF.Sin(theta);
                    }
                    return values;
                }

                if (indices.Drums.Length  > 0) panning.Drums  = CalculateStemValues(indices.Drums);
                if (indices.Bass.Length   > 0) panning.Bass   = CalculateStemValues(indices.Bass);
                if (indices.Guitar.Length > 0) panning.Guitar = CalculateStemValues(indices.Guitar);
                if (indices.Keys.Length   > 0) panning.Keys   = CalculateStemValues(indices.Keys);
                if (indices.Vocals.Length > 0) panning.Vocals = CalculateStemValues(indices.Vocals);
                if (indices.Track.Length  > 0) panning.Track  = CalculateStemValues(indices.Track);
                if (indices.Crowd.Length  > 0) panning.Crowd  = CalculateStemValues(indices.Crowd);

                return true;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                return false;
            }
        }
    }
}
