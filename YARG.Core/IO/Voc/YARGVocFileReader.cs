using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Song;
using LipsyncType = YARG.Core.Chart.LipsyncEvent.LipsyncType;

namespace YARG.Core.IO.Voc
{
    public static class YARGVocFileReader
    {
        private enum VocVisemeType
        {
            // Lipsync
            Neutral,
            Eat,
            Earth,
            If,
            Ox,
            Oat,
            Wet,
            Size,
            Church,
            Fave,
            Though,
            Told,
            Bump,
            New,
            Roar,
            Cage,
            // Facial expressions
            EyebrowRaise,
            // We don't do anything with these, but in case they're useful:
            OrientationHeadPitch,
            OrientationHeadRoll,
            OrientationHeadYaw,
            GazeEyePitch,
            GazeEyeYaw,
            EmphasisHeadPitch,
            EmphasisHeadRoll,
            EmphasisHeadYaw,
            Intensity
            // There are more, but they are shared with the milo viseme types, so we can just use those instead of duplicating them here.
        }

        private static readonly Dictionary<VocVisemeType, Tuple<LipsyncType, LipsyncType>> VocLipsyncVisemeLookup =
            new()
            {
                {
                    VocVisemeType.Neutral,
                    new Tuple<LipsyncType, LipsyncType>(LipsyncType.Neutral_hi, LipsyncType.Neutral_lo)
                },
                {
                    VocVisemeType.Eat, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Eat_hi, LipsyncType.Eat_lo)
                },
                {
                    VocVisemeType.Earth, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Earth_hi, LipsyncType.Earth_lo)
                },
                {
                    VocVisemeType.If, new Tuple<LipsyncType, LipsyncType>(LipsyncType.If_hi, LipsyncType.If_lo)
                },
                {
                    VocVisemeType.Ox, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Ox_hi, LipsyncType.Ox_lo)
                },
                {
                    VocVisemeType.Oat, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Oat_hi, LipsyncType.Oat_lo)
                },
                {
                    VocVisemeType.Wet, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Wet_hi, LipsyncType.Wet_lo)
                },
                {
                    VocVisemeType.Size, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Size_hi, LipsyncType.Size_lo)
                },
                {
                    VocVisemeType.Church,
                    new Tuple<LipsyncType, LipsyncType>(LipsyncType.Church_hi, LipsyncType.Church_lo)
                },
                {
                    VocVisemeType.Fave, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Fave_hi, LipsyncType.Fave_lo)
                },
                {
                    VocVisemeType.Though,
                    new Tuple<LipsyncType, LipsyncType>(LipsyncType.Though_hi, LipsyncType.Though_lo)
                },
                {
                    VocVisemeType.Told, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Told_hi, LipsyncType.Told_lo)
                },
                {
                    VocVisemeType.Bump, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Bump_hi, LipsyncType.Bump_lo)
                },
                {
                    VocVisemeType.New, new Tuple<LipsyncType, LipsyncType>(LipsyncType.New_hi, LipsyncType.New_lo)
                },
                {
                    VocVisemeType.Roar, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Roar_hi, LipsyncType.Roar_lo)
                },
                {
                    VocVisemeType.Cage, new Tuple<LipsyncType, LipsyncType>(LipsyncType.Cage_hi, LipsyncType.Cage_lo)
                },
            };
        private static readonly Dictionary<VocVisemeType, LipsyncType> VocOtherVisemeLookup = new()
        {
            {
                VocVisemeType.EyebrowRaise, LipsyncType.Brow_up
            },
        };

        private struct VocHeader
        {
            // 10 bytes of unknown data,
            // 4 bytes for name length, then developer name,
            public string DeveloperName;

            // 2 bytes of unknown padding
            // 4 bytes for game metadata length, then game metadata,
            public string GameMetadata;

            // 12 bytes of unknown padding
            // 4 bytes for song name length, then song name,
            public string SongName;

            // 2 bytes of unknown padding
            // 4-byte int for file size
            public int FileSize;

            // 2 bytes of unknown padding
            // 4-byte int for viseme count
            public int VisemeCount;
        }

        private struct VocViseme
        {
            // 8 bytes of unknown data,
            // 4-byte int for viseme name length, then viseme name,
            public string VisemeName;

            // 8 bytes of padding
            // 4-byte int for event count, then event data
            public List<VocVisemeEvent> Events;
        }

        private struct VocVisemeEvent
        {
            // 2 bytes of unknown data,
            // 4-byte LE float for time,
            public float Time;

            // 4-byte LE float for value (0-1)
            public float Value;
            // 8 bytes of unknown padding
        }

        public static List<LipsyncEvent> GetVocExpressions(SongChart chart, SongEntry entry)
        {
            var data = entry.LoadVocData();
            if (data == null)
            {
                return new List<LipsyncEvent>();
            }

            using var reader = new BinaryReader(data.ToReferenceStream());
            var header = ReadHeader(reader);
            var visemes = new VocViseme?[header.VisemeCount];

            for (var i = 0; i < header.VisemeCount; i++)
            {
                visemes[i] = ReadViseme(reader);
            }

            var lipsyncEvents = new List<LipsyncEvent>();
            foreach (var viseme in visemes)
            {
                if (viseme != null)
                {
                    lipsyncEvents.AddRange(ConvertVocVisemeToLipsyncEvents(viseme.Value, chart.SyncTrack));
                }
            }

            lipsyncEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            return lipsyncEvents;
        }

        private static List<LipsyncEvent> ConvertVocVisemeToLipsyncEvents(VocViseme viseme, SyncTrack syncTrack)
        {
            var lipsyncEvents = new List<LipsyncEvent>();

            foreach (var vocEvent in viseme.Events)
            {
                if (Enum.TryParse(viseme.VisemeName, out MiloLipsync.Visemes miloViseme))
                {
                    if (MiloVenue.VisemeLookup.TryGetValue(miloViseme, out var lipsyncType))
                    {
                        lipsyncEvents.Add(new LipsyncEvent(lipsyncType, vocEvent.Value, vocEvent.Time,
                            syncTrack.TimeToTick(vocEvent.Time)));
                    }
                }
                else if (Enum.TryParse(viseme.VisemeName, out VocVisemeType vocVisemeType))
                {
                    if (VocLipsyncVisemeLookup.TryGetValue(vocVisemeType, out var lipsyncTypes))
                    {
                        // This matches how RB-Tools does it...
                        lipsyncEvents.Add(new LipsyncEvent(lipsyncTypes.Item1, vocEvent.Value * 0.66f, vocEvent.Time,
                            syncTrack.TimeToTick(vocEvent.Time)));
                        lipsyncEvents.Add(new LipsyncEvent(lipsyncTypes.Item2, vocEvent.Value * 0.33f, vocEvent.Time,
                            syncTrack.TimeToTick(vocEvent.Time)));
                    }
                    else if (VocOtherVisemeLookup.TryGetValue(vocVisemeType, out var lipsyncType))
                    {
                        lipsyncEvents.Add(new LipsyncEvent(lipsyncType, vocEvent.Value, vocEvent.Time,
                            syncTrack.TimeToTick(vocEvent.Time)));
                    }
                }
            }

            return lipsyncEvents;
        }

        private static VocHeader ReadHeader(BinaryReader reader)
        {
            var header = new VocHeader();

            reader.BaseStream.Seek(10, SeekOrigin.Begin);

            var developerNameLength = reader.ReadInt32();
            header.DeveloperName = new string(reader.ReadChars(developerNameLength));

            reader.BaseStream.Seek(2, SeekOrigin.Current);

            var gameMetadataLength = reader.ReadInt32();
            header.GameMetadata = new string(reader.ReadChars(gameMetadataLength));

            reader.BaseStream.Seek(12, SeekOrigin.Current);

            var songNameLength = reader.ReadInt32();
            header.SongName = new string(reader.ReadChars(songNameLength));

            reader.BaseStream.Seek(2, SeekOrigin.Current);

            header.FileSize = reader.ReadInt32();

            reader.BaseStream.Seek(2, SeekOrigin.Current);

            header.VisemeCount = reader.ReadInt32();

            return header;
        }

        private static VocViseme? ReadViseme(BinaryReader reader)
        {
            reader.BaseStream.Seek(8, SeekOrigin.Current); // Skip 8 bytes of unknown data

            var visemeNameLength = reader.ReadInt32();
            // Slight hack, because the voc viseme names can have spaces in them, but the enum values don't. So we remove the spaces before trying to parse it as a VocVisemeType
            var visemeName = new string(reader.ReadChars(visemeNameLength)).Replace(" ", "");
            if (!Enum.TryParse<MiloLipsync.Visemes>(visemeName, out _) &&
                !Enum.TryParse<VocVisemeType>(visemeName, out _))
            {
                // Seek past the rest of the viseme data to continue reading the next viseme
                reader.BaseStream.Seek(8, SeekOrigin.Current); // Skip 8 bytes of padding
                var count = reader.ReadInt32();
                reader.BaseStream.Seek(count * (2 + 4 + 4 + 8), SeekOrigin.Current); // Skip the event data
                YargLogger.LogFormatWarning("Unknown viseme name: {0}, skipping", visemeName);
                return null;
            }

            var viseme = new VocViseme
            {
                VisemeName = visemeName
            };

            reader.BaseStream.Seek(8, SeekOrigin.Current); // Skip 8 bytes of padding

            var eventCount = reader.ReadInt32();
            viseme.Events = new List<VocVisemeEvent>(eventCount);

            for (var i = 0; i < eventCount; i++)
            {
                viseme.Events.Add(ReadVisemeEvent(reader));
            }

            return viseme;
        }

        private static VocVisemeEvent ReadVisemeEvent(BinaryReader reader)
        {
            var vocEvent = new VocVisemeEvent();
            reader.BaseStream.Seek(2, SeekOrigin.Current); // Skip 2 bytes of unknown data
            vocEvent.Time = reader.ReadSingle();
            vocEvent.Value = reader.ReadSingle();
            reader.BaseStream.Seek(8, SeekOrigin.Current); // Skip 8 bytes of unknown padding
            return vocEvent;
        }
    }
}