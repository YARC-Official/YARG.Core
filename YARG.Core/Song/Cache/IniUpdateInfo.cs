using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    /// <summary>
    /// Everything found in a shortname's songs_updates/&lt;shortname&gt; folder that can be applied
    /// to an ini-format song: the update midi (already supported), plus the update mogg, album art,
    /// and whatever DTA-declared audio channel/panning info exists for that shortname.
    /// </summary>
    internal struct IniUpdateInfo
    {
        public string? MidiPath;
        public string? MoggPath;
        public string? ImagePath;
        public DTAEntry Dta;
    }

    /// <summary>
    /// Applies the metadata-relevant fields of a songs_updates.dta entry onto an ini song's
    /// SongMetadata. Mirrors the metadata portion of RBCONEntry.ParseDTA, but only the fields
    /// SongMetadata actually has — RBCON-only concerns (rank/intensity, hopo threshold, tuning,
    /// venue metadata, etc.) don't apply to ini songs and are left alone.
    /// </summary>
    internal static class IniDtaMetadataApplier
    {
        public static void Apply(in DTAEntry dta, ref SongMetadata metadata)
        {
            if (dta.Name != null)          { metadata.Name          = YARGDTAReader.DecodeString(dta.Name.Value, dta.MetadataEncoding); }
            if (dta.Artist != null)        { metadata.Artist        = YARGDTAReader.DecodeString(dta.Artist.Value, dta.MetadataEncoding); }
            if (dta.CoveredBy != null)     { metadata.CoveredBy     = YARGDTAReader.DecodeString(dta.CoveredBy.Value, dta.MetadataEncoding); }
            if (dta.Album != null)         { metadata.Album         = YARGDTAReader.DecodeString(dta.Album.Value, dta.MetadataEncoding); }
            if (dta.Charter != null)       { metadata.Charter       = YARGDTAReader.DecodeString(dta.Charter.Value, dta.MetadataEncoding); }
            if (dta.LoadingPhrase != null) { metadata.LoadingPhrase = YARGDTAReader.DecodeString(dta.LoadingPhrase.Value, dta.MetadataEncoding); }
            if (dta.Playlist != null)      { metadata.Playlist      = YARGDTAReader.DecodeString(dta.Playlist.Value, dta.MetadataEncoding); }

            if (dta.Genre != null)
            {
                metadata.Genre = dta.Genre;
                metadata.Subgenre = string.Empty;
            }
            if (dta.Subgenre != null) { metadata.Subgenre = dta.Subgenre.Replace("subgenre_", ""); }

            if (dta.YearAsNumber != null)          { metadata.Year          = dta.YearAsNumber.Value.ToString("D4"); }
            if (dta.YearSecondaryAsNumber != null) { metadata.YearSecondary = dta.YearSecondaryAsNumber.Value.ToString("D4"); }

            if (dta.Source != null)
            {
                // Simplified from RBCONEntry's version, which also special-cases "UGC_"-prefixed
                // node names — not meaningful for ini shortnames.
                metadata.Source = dta.Source is "ugc" or "ugc_plus" ? "customs" : dta.Source;
            }

            if (dta.SongLength != null)  { metadata.SongLength = dta.SongLength.Value; }
            if (dta.IsMaster != null)    { metadata.IsMaster    = dta.IsMaster.Value; }
            if (dta.AlbumTrack != null)  { metadata.AlbumTrack  = dta.AlbumTrack.Value; }
            if (dta.Preview != null)     { metadata.Preview     = dta.Preview.Value; }
            if (dta.SongRating != null)  { metadata.SongRating  = dta.SongRating.Value; }
            if (dta.VocalGender != null) { metadata.VocalGender = DTAEntry.ConvertVocalGender(dta.VocalGender); }
        }
    }

    internal static class RBAudioCalculator
    {
        /// <summary>
        /// Reads channel indices/crowd channels out of <paramref name="dta"/> and computes the
        /// per-channel pan/volume values used to split a mogg into stems. Mirrors the math in
        /// RBCONEntry.ProcessDTAs, but standalone (ini entries don't carry the rest of the RBCON
        /// metadata that ProcessDTAs also touches).
        /// </summary>
        public static void Calculate(in DTAEntry dta, ref RBAudio<int> indices, ref RBAudio<float> panning)
        {
            if (dta.Indices != null)
            {
                indices = dta.Indices.Value;
            }

            // Explicit crowd_channels always wins over whatever indices.Crowd was
            if (dta.CrowdChannels != null)
            {
                indices.Crowd = dta.CrowdChannels;
            }

            var pans = dta.Pans;
            var volumes = dta.Volumes;
            if (pans == null || volumes == null)
            {
                return;
            }

            unsafe
            {
                var usedIndices = stackalloc bool[pans.Length];
                float[] CalculateStemValues(int[] stemIndices)
                {
                    float[] values = new float[2 * stemIndices.Length];
                    for (int i = 0; i < stemIndices.Length; i++)
                    {
                        int index = stemIndices[i];
                        if (index < 0 || index >= pans.Length || index >= volumes.Length)
                        {
                            continue;
                        }

                        float theta = (pans[index] + 1) * ((float) Math.PI / 4);
                        float volRatio = (float) Math.Pow(10, volumes[index] / 20);
                        values[2 * i] = volRatio * (float) Math.Cos(theta);
                        values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                        usedIndices[index] = true;
                    }
                    return values;
                }

                if (indices.Drums.Length  > 0) panning.Drums  = CalculateStemValues(indices.Drums);
                if (indices.Bass.Length   > 0) panning.Bass   = CalculateStemValues(indices.Bass);
                if (indices.Guitar.Length > 0) panning.Guitar = CalculateStemValues(indices.Guitar);
                if (indices.Keys.Length   > 0) panning.Keys   = CalculateStemValues(indices.Keys);
                if (indices.Vocals.Length > 0) panning.Vocals = CalculateStemValues(indices.Vocals);
                if (indices.Crowd.Length  > 0) panning.Crowd  = CalculateStemValues(indices.Crowd);

                var leftover = new List<int>(pans.Length);
                for (int i = 0; i < pans.Length; i++)
                {
                    if (!usedIndices[i])
                    {
                        leftover.Add(i);
                    }
                }

                if (leftover.Count > 0)
                {
                    indices.Track = leftover.ToArray();
                    panning.Track = CalculateStemValues(indices.Track);
                }
            }
        }
    }

    /// <summary>
    /// Splits an opened, seeked mogg stream into stem channels on a mixer, using the given channel
    /// indices/panning. Self-contained copy of the same splitting logic RBCONEntry.LoadAudio uses,
    /// kept separate so ini-only work doesn't need to touch SongEntry.RBCON.cs.
    /// </summary>
    internal static class IniMoggStemSplitter
    {
        public static bool AddMoggStems(StemMixer mixer, Stream stream, in RBAudio<int> indices, in RBAudio<float> panning, SongStem[] ignoreStems)
        {
            var stemInfos = new List<StemMixer.StemInfo>();

            if (indices.Drums.Length > 0 && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 1:
                    case 2:
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums, panning.Drums));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[1..3], panning.Drums[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[1..2], panning.Drums[2..4]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[2..4], panning.Drums[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[0..1], panning.Drums[0..2]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[1..3], panning.Drums[2..6]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[3..5], panning.Drums[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[0..2], panning.Drums[0..4]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[2..4], panning.Drums[4..8]));
                        stemInfos.Add(new StemMixer.StemInfo(SongStem.Drums, indices.Drums[4..6], panning.Drums[8..12]));
                        break;
                }
            }

            if (indices.Bass.Length > 0 && !ignoreStems.Contains(SongStem.Bass))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Bass, indices.Bass, panning.Bass));

            if (indices.Guitar.Length > 0 && !ignoreStems.Contains(SongStem.Guitar))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Guitar, indices.Guitar, panning.Guitar));

            if (indices.Keys.Length > 0 && !ignoreStems.Contains(SongStem.Keys))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Keys, indices.Keys, panning.Keys));

            if (indices.Vocals.Length > 0 && !ignoreStems.Contains(SongStem.Vocals))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Vocals, indices.Vocals, panning.Vocals));

            if (indices.Track.Length > 0 && !ignoreStems.Contains(SongStem.Song))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Song, indices.Track, panning.Track));

            if (indices.Crowd.Length > 0 && !ignoreStems.Contains(SongStem.Crowd))
                stemInfos.Add(new StemMixer.StemInfo(SongStem.Crowd, indices.Crowd, panning.Crowd));

            mixer.AddChannels(stream, stemInfos.ToArray());
            return mixer.Channels.Count > 0;
        }
    }

    /// <summary>
    /// Local (de)serialization of RBAudio&lt;T&gt; for ini cache entries — a copy of the same
    /// read/write pattern RBCONEntry uses for its own cache entries, kept local so this doesn't
    /// require touching SongEntry.RBCON.cs's private helpers.
    /// </summary>
    internal static class IniAudioSerializer
    {
        public static void WriteArray<TType>(in TType[] values, MemoryStream stream)
            where TType : unmanaged
        {
            stream.Write(values.Length, Endianness.Little);
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    var span = new ReadOnlySpan<byte>(ptr, values.Length * sizeof(TType));
                    stream.Write(span);
                }
            }
        }

        public static TType[] ReadArray<TType>(ref FixedArrayStream stream)
            where TType : unmanaged
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<TType>();
            }

            var values = new TType[length];
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    stream.Read(ptr, values.Length * sizeof(TType));
                }
            }
            return values;
        }

        public static void WriteAudio<TType>(in RBAudio<TType> audio, MemoryStream stream)
            where TType : unmanaged
        {
            WriteArray(in audio.Track, stream);
            WriteArray(in audio.Drums, stream);
            WriteArray(in audio.Bass, stream);
            WriteArray(in audio.Guitar, stream);
            WriteArray(in audio.Keys, stream);
            WriteArray(in audio.Vocals, stream);
            WriteArray(in audio.Crowd, stream);
        }

        public static void ReadAudio<TType>(ref RBAudio<TType> audio, ref FixedArrayStream stream)
            where TType : unmanaged
        {
            audio.Track  = ReadArray<TType>(ref stream);
            audio.Drums  = ReadArray<TType>(ref stream);
            audio.Bass   = ReadArray<TType>(ref stream);
            audio.Guitar = ReadArray<TType>(ref stream);
            audio.Keys   = ReadArray<TType>(ref stream);
            audio.Vocals = ReadArray<TType>(ref stream);
            audio.Crowd  = ReadArray<TType>(ref stream);
        }
    }
}
