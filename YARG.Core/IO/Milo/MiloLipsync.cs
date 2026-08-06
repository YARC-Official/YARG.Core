using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Venue;

namespace YARG.Core.IO
{
    public class MiloLipsync : IDisposable
    {
        private const string MILO_LIPSYNC_FILE = "song.lipsync";

        private readonly List<FixedArray<byte>> _lipsyncDataList;
        private readonly FixedArray<byte> _bandSongPref;
        private          bool                   _disposed;

        public MiloLipsync(FixedArray<byte> miloFile)
        {
            _lipsyncDataList = new List<FixedArray<byte>>(3);
            // No extension because Harmonix is weird
            _bandSongPref = YARGMiloReader.GetMiloFile(miloFile, "BandSongPref");

            // Read the lipsync data for each part
            _lipsyncDataList.Add(YARGMiloReader.GetMiloFile(miloFile, MILO_LIPSYNC_FILE));
            int currentPart = 2; // starts at 2
            while (true)
            {
                var lipsync = YARGMiloReader.GetMiloFile(miloFile, $"part{currentPart}.lipsync");
                if (lipsync.Length == 0)
                {
                    break;
                }
                _lipsyncDataList.Add(lipsync);
                currentPart++;
            }
        }
        /// <summary>
        /// Gets Milo lipsync data for a given chart, if present.
        /// </summary>
        /// <returns>An array of <see cref="VisemeData"/> lists, indexed by lipsync part. <br/>
        /// Index 0 = song.lipsync, otherwise index n = part{n+1}.lipsync </returns>
        public List<VisemeData>[] GetLipsyncData()
        {
            var harmonyData = new List<VisemeData>[_lipsyncDataList.Count];
            for (int i = 0; i < _lipsyncDataList.Count; i++)
            {
                harmonyData[i] = GetLipsyncDataForPart(i);
            }
            return harmonyData;
        }

        public Performer[] GetSingerPreferenceFromMilo()
        {
            if (_bandSongPref.Length == 0)
            {
                return new Performer[4];
            }
            /*
             * 0x14 => Length of Part2Instrument
             * 0x15-0x15+Length => Part2Instrument
             * 0x1C => Length of Part3Instrument
             * 0x1D-0x1D+Length => Part3Instrument
             * 0x24 => Length of Part4Instrument
             * 0x25-0x25+Length => Part4Instrument
             * 0x2E => Length of AnimationGenre
             * 0x2F-0x2F+Length => AnimationGenre
             */
            return new Performer[]
            {
                Performer.Vocals,
                GetPerformerFromBandSongPrefBytes(_bandSongPref, 0x14),
                GetPerformerFromBandSongPrefBytes(_bandSongPref, 0x1C),
                GetPerformerFromBandSongPrefBytes(_bandSongPref, 0x24)
            };
        }

        /// <summary>
        /// Get the performer from the bytes in a BandSongPref milo file.
        /// </summary>
        /// <param name="data">The bytes in the BandSongPref file.</param>
        /// <param name="offset">The start of the performer string entry, including the length prefix.</param>
        /// <returns>The listed performer, or </returns>
        private static Performer GetPerformerFromBandSongPrefBytes(in FixedArray<byte> data, int offset)
        {
            if (data.Length < offset + 4)
            {
                return Performer.None;
            }
            var length = data[offset];
            var instrument = Encoding.UTF8.GetString(data.Slice(offset + 1, length).ToArray());
            return instrument.ToLower() switch
            {
                "guitar" => Performer.Guitar,
                "bass"   => Performer.Bass,
                "drum"   => Performer.Drums,
                _        => Performer.None
            };
        }

        private List<VisemeData> GetLipsyncDataForPart(int partIndex)
        {
            var data = _lipsyncDataList[partIndex];
            if (data.Length == 0)
            {
                data.Dispose();
                YargLogger.LogFormatWarning("Milo file does not contain lipsync data for part {0}", partIndex);
                return new List<VisemeData>();
            }

            // Why 8? I have no idea
            var bufferIndex = 8;
            byte[] fourBytes = new byte[4];

            // Read four bytes from data starting at bufferIndex into fourBytes
            data.Slice(bufferIndex, 4).CopyTo(fourBytes);

            // Parse the four bytes into a uint and add 17 to get the start
            var start = BinaryPrimitives.ReadUInt32LittleEndian(fourBytes) + 17;
            bufferIndex = (int) start;

            // From now on we're working in big endian

            // Read a uint at start to get the count of visemes in this file
            var visemeCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice((int) bufferIndex, 4));
            bufferIndex += 4;

            // Allocate an array of Viseme, which will serve as our ordered list referenced in the frame data
            var visemeIndex = new Visemes[visemeCount];

            for (int i = 0; i < visemeCount; i++)
            {
                // Read a uint denoting the length of the name
                var nameLength = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(bufferIndex, 4));
                bufferIndex += 4;

                var visemeName = data.Slice(bufferIndex, (int) nameLength).ToArray();
                bufferIndex += (int) nameLength;

                // Parse the viseme name into a Viseme enum value
                if (Enum.TryParse<Visemes>(Encoding.UTF8.GetString(visemeName), out var viseme))
                {
                    visemeIndex[i] = viseme;
                }
                else
                {
                    YargLogger.LogFormatError("Failed to parse viseme name {0}", Encoding.UTF8.GetString(visemeName));
                }
            }

            // Next read a uint for the frame count
            var frameCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(bufferIndex, 4));
            bufferIndex += 4;

            // And one more for visemeElements, whatever that is
            var visemeElementsCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(bufferIndex, 4));
            bufferIndex += 4;

            // I think we're being told we will have visemeElementsCount viseme updates?
            var visemeData = new List<VisemeData>((int) visemeElementsCount);

            for (var i = 0; i < frameCount; i++)
            {
                // Read one ushort
                var frameChanges = (int) data[bufferIndex];
                bufferIndex++;

                if (frameChanges == 0)
                {
                    // No change this frame, so do nothing
                    continue;
                }

                // Read frameChanges changes, creating a Viseme struct for each
                for (var j = 0; j < frameChanges; j++)
                {
                    var idx = (int) data[bufferIndex];
                    bufferIndex++;
                    var value = (int) data[bufferIndex];
                    bufferIndex++;

                    var viseme = new VisemeData
                    {
                        Viseme = visemeIndex[idx],
                        StartTime = i / 30.0,
                        Value = value
                    };
                    visemeData.Add(viseme);
                }
            }

            return visemeData;
        }

        public struct VisemeData
        {
            public Visemes Viseme;
            public double  StartTime;     // Frame number / 30 to get seconds
            public byte[]  FrameDataName; // I have no idea yet what this is
            public int     Value;
        }

        public enum Visemes
        {
            // Actual visemes
            // ReSharper disable InconsistentNaming
            Bump_hi,
            Bump_lo,
            Cage_hi,
            Cage_lo,
            Church_hi,
            Church_lo,
            Earth_hi,
            Earth_lo,
            Eat_hi,
            Eat_lo,
            Fave_hi,
            Fave_lo,
            If_hi,
            If_lo,
            Neutral_hi,
            Neutral_lo,
            New_hi,
            New_lo,
            Oat_hi,
            Oat_lo,
            Ox_hi,
            Ox_lo,
            Roar_hi,
            Roar_lo,
            Size_hi,
            Size_lo,
            Though_hi,
            Though_lo,
            Told_hi,
            Told_lo,
            Wet_hi,
            Wet_lo,
            // Other facial animation stuff
            Blink,
            Brow_aggressive,
            Brow_down,
            Brow_dramatic,
            Brow_openmouthed,
            Brow_pouty,
            Brow_up,
            Squint,
            Wide_eyed,
            // Complex expressions
            exp_spazz_tongueout_side_01,
            exp_spazz_tongueout_front_01,
            exp_spazz_snear_mellow_01,
            exp_spazz_snear_intense_01,
            exp_spazz_eyesclosed_01,
            exp_rocker_teethgrit_pained_01,
            exp_rocker_teethgrit_happy_01,
            exp_rocker_soloface_01,
            exp_rocker_smile_mellow_01,
            exp_rocker_smile_intense_01,
            exp_rocker_slackjawed_01,
            exp_rocker_shout_quick_01,
            exp_rocker_shout_eyesopen_01,
            exp_rocker_shout_eyesclosed_01,
            exp_rocker_bassface_cool_01,
            exp_rocker_bassface_aggressive_01,
            exp_dramatic_pouty_01,
            exp_dramatic_mouthopen_01,
            exp_dramatic_happy_eyesopen_01,
            exp_dramatic_happy_eyesclosed_01,
            exp_banger_teethgrit_01,
            exp_banger_slackjawed_01,
            exp_banger_roar_01,
            exp_banger_oohface_01
            // ReSharper restore InconsistentNaming
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // In case we need it later
                }

                // This is treated as if it is unmanaged since it is wrapping unmanaged memory
                foreach (var data in _lipsyncDataList)
                {
                    data.Dispose();
                }
                _lipsyncDataList.Clear();

                _disposed = true;
            }
        }

        ~MiloLipsync()
        {
            Dispose(disposing: false);
        }
    }
}