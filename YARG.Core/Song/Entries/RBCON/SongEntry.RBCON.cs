using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Extensions;
using YARG.Core.Audio;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public sealed class DTAResult
    {
        public readonly string nodeName;
        public bool alternatePath = false;
        public bool discUpdate = false;
        public string location = string.Empty;
        public float[] pans = Array.Empty<float>();
        public float[] volumes = Array.Empty<float>();
        public float[] cores = Array.Empty<float>();

        public DTAResult(string nodeName)
        {
            this.nodeName = nodeName;
        }
    }

    public abstract class RBCONEntry : SongEntry
    {
        public readonly RBCONDifficulties RBDifficulties = new();

        public string SongID = string.Empty;
        public uint AnimTempo;
        public string DrumBank = string.Empty;
        public string VocalPercussionBank = string.Empty;
        public uint VocalSongScrollSpeed;
        public uint SongRating; // 1 = FF; 2 = SR; 3 = M; 4 = NR
        public bool VocalGender = true;//true for male, false for female
        public bool HasAlbumArt;
        //public bool IsFake;
        public uint VocalTonicNote;
        public bool SongTonality; // 0 = major, 1 = minor
        public int TuningOffsetCents;
        public uint VenueVersion;

        public AbridgedFileInfo? UpdateMidi = null;
        public AbridgedFileInfo? UpdateMogg = null;
        public AbridgedFileInfo? UpdateMilo = null;
        public AbridgedFileInfo? UpdateImage = null;

        public IRBProUpgrade? Upgrade;

        public string[] Soloes = Array.Empty<string>();
        public string[] VideoVenues = Array.Empty<string>();

        public int[] RealGuitarTuning = Array.Empty<int>();
        public int[] RealBassTuning = Array.Empty<int>();

        public int[] DrumIndices = Array.Empty<int>();
        public int[] BassIndices = Array.Empty<int>();
        public int[] GuitarIndices = Array.Empty<int>();
        public int[] KeysIndices = Array.Empty<int>();
        public int[] VocalsIndices = Array.Empty<int>();
        public int[] CrowdIndices = Array.Empty<int>();
        public int[] TrackIndices = Array.Empty<int>();

        public float[] TrackStemValues = Array.Empty<float>();
        public float[] DrumStemValues = Array.Empty<float>();
        public float[] BassStemValues = Array.Empty<float>();
        public float[] GuitarStemValues = Array.Empty<float>();
        public float[] KeysStemValues = Array.Empty<float>();
        public float[] VocalsStemValues = Array.Empty<float>();
        public float[] CrowdStemValues = Array.Empty<float>();

        protected abstract DateTime MidiLastWrite { get; }

        public DateTime GetAddTime(DateTime lastUpdateTime)
        {
            if (UpdateMidi != null)
            {
                if (UpdateMidi.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = UpdateMidi.LastUpdatedTime;
                }
            }

            if (Upgrade != null)
            {
                if (Upgrade.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = Upgrade.LastUpdatedTime;
                }
            }
            return lastUpdateTime;
        }

        public override SongChart? LoadChart()
        {
            MidiFile midi;
            var readingSettings = MidiSettingsLatin1.Instance; // RBCONs are always Latin-1
            // Read base MIDI
            using (var midiStream = GetMidiStream())
            {
                if (midiStream == null)
                    return null;
                midi = MidiFile.Read(midiStream, readingSettings);
            }

            // Merge update MIDI
            if (UpdateMidi != null)
            {
                if (!UpdateMidi.IsStillValid(false))
                    return null;

                using var midiStream = new FileStream(UpdateMidi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (Upgrade != null)
            {
                using var midiStream = Upgrade.GetUpgradeMidiStream();
                if (midiStream == null)
                    return null;
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            return SongChart.FromMidi(Metadata.ParseSettings, midi);
        }

        public override List<AudioChannel> LoadAudioStreams(params SongStem[] ignoreStems)
        {
            var channels = new List<AudioChannel>();
            int version = GetMoggVersion();

            Func<SongStem, int[], float[], AudioChannel?>? func = null;
            switch (version)
            {
                case 0x0A:
                    func = InitMoggFunc();
                    break;
                case 0xF0:
                    func = InitYARGMoggFunc();
                    break;
                default:
                    YargTrace.LogError("Original unencrypted mogg replaced by an encrypted mogg");
                    break;
            }

            if (func == null)
            {
                return channels;
            }

            void Add(SongStem stem, int[] indices, float[]panning)
            {
                var channel = func(stem, indices, panning);
                if (channel != null)
                {
                    channels.Add(channel);
                }
            }

            if (DrumIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        Add(SongStem.Drums, DrumIndices, DrumStemValues);
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        Add(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]);
                        Add(SongStem.Drums2, DrumIndices[1..3], DrumStemValues[2..6]);
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        Add(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]);
                        Add(SongStem.Drums2, DrumIndices[1..2], DrumStemValues[2..4]);
                        Add(SongStem.Drums3, DrumIndices[2..4], DrumStemValues[4..8]);
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        Add(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]);
                        Add(SongStem.Drums2, DrumIndices[1..3], DrumStemValues[2..6]);
                        Add(SongStem.Drums3, DrumIndices[3..5], DrumStemValues[6..10]);
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        Add(SongStem.Drums1, DrumIndices[0..2], DrumStemValues[0..4]);
                        Add(SongStem.Drums2, DrumIndices[2..4], DrumStemValues[4..8]);
                        Add(SongStem.Drums3, DrumIndices[4..6], DrumStemValues[8..12]);
                        break;
                }
            }

            if (BassIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Bass))
                Add(SongStem.Bass, BassIndices, BassStemValues);

            if (GuitarIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Guitar))
                Add(SongStem.Guitar, GuitarIndices, GuitarStemValues);

            if (KeysIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Keys))
                Add(SongStem.Keys, KeysIndices, KeysStemValues);

            if (VocalsIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Vocals))
                Add(SongStem.Vocals, VocalsIndices, VocalsStemValues);

            if (TrackIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Song))
                Add(SongStem.Song, TrackIndices, TrackStemValues);

            if (CrowdIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Crowd))
                Add(SongStem.Crowd, CrowdIndices, CrowdStemValues);
            return channels;
        }

        public override List<AudioChannel> LoadPreviewAudio()
        {
            return LoadAudioStreams(SongStem.Crowd);
        }

        public override byte[]? LoadAlbumData()
        {
            var bytes = LoadRawImageData();
            if (bytes == null)
            {
                return null;
            }

            for (int i = 32; i < bytes.Length; i += 2)
            {
                (bytes[i + 1], bytes[i]) = (bytes[i], bytes[i + 1]);
            }
            return bytes;
        }

        public override byte[]? LoadMiloData()
        {
            if (UpdateMilo != null && UpdateMilo.Exists())
            {
                return File.ReadAllBytes(UpdateMilo.FullName);
            }
            return null;
        }

        protected abstract void SerializeSubData(BinaryWriter writer);

        public void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            SerializeSubData(writer);
            SerializeMetadata(writer, node);

            RBDifficulties.Serialize(writer);

            writer.Write(AnimTempo);
            writer.Write(SongID);
            writer.Write(VocalPercussionBank);
            writer.Write(VocalSongScrollSpeed);
            writer.Write(SongRating);
            writer.Write(VocalGender);
            writer.Write(VocalTonicNote);
            writer.Write(SongTonality);
            writer.Write(TuningOffsetCents);
            writer.Write(VenueVersion);

            WriteUpdateInfo(UpdateMogg, writer);
            WriteUpdateInfo(UpdateMilo, writer);
            WriteUpdateInfo(UpdateImage, writer);

            WriteArray(RealGuitarTuning, writer);
            WriteArray(RealBassTuning, writer);

            WriteArray(DrumIndices, writer);
            WriteArray(BassIndices, writer);
            WriteArray(GuitarIndices, writer);
            WriteArray(KeysIndices, writer);
            WriteArray(VocalsIndices, writer);
            WriteArray(TrackIndices, writer);
            WriteArray(CrowdIndices, writer);

            WriteArray(DrumStemValues, writer);
            WriteArray(BassStemValues, writer);
            WriteArray(GuitarStemValues, writer);
            WriteArray(KeysStemValues, writer);
            WriteArray(VocalsStemValues, writer);
            WriteArray(TrackStemValues, writer);
            WriteArray(CrowdStemValues, writer);
        }

        protected virtual byte[]? LoadRawImageData()
        {
            if (UpdateImage != null && UpdateImage.Exists())
            {
                return File.ReadAllBytes(UpdateImage.FullName);
            }
            return null;
        }

        protected virtual Stream? GetMoggStream()
        {
            if (UpdateMogg == null || !File.Exists(UpdateMogg.FullName))
            {
                return null;
            }

            if (UpdateMogg.FullName.EndsWith(".yarg_mogg"))
            {
                return new YargMoggReadStream(UpdateMogg.FullName);
            }
            return new FileStream(UpdateMogg.FullName, FileMode.Open, FileAccess.Read);
        }

        protected virtual bool IsMoggValid(CONFile? file)
        {
            using var stream = GetMoggStream();
            if (stream != null)
            {
                int version = stream.Read<int>(Endianness.Little);
                return version == 0x0A || version == 0xf0;
            }
            return false;
        }

        protected abstract byte[]? LoadMidiFile(CONFile? file);

        protected abstract Stream? GetMidiStream();

        protected RBCONEntry() : base(SongMetadata.Default) { }

        protected RBCONEntry(AbridgedFileInfo? updateMidi, in SongMetadata metadata, BinaryReader reader)
            : base(metadata)
        {
            UpdateMidi = updateMidi;

            RBDifficulties = new RBCONDifficulties(reader);

            AnimTempo = reader.ReadUInt32();
            SongID = reader.ReadString();
            VocalPercussionBank = reader.ReadString();
            VocalSongScrollSpeed = reader.ReadUInt32();
            SongRating = reader.ReadUInt32();
            VocalGender = reader.ReadBoolean();
            VocalTonicNote = reader.ReadUInt32();
            SongTonality = reader.ReadBoolean();
            TuningOffsetCents = reader.ReadInt32();
            VenueVersion = reader.ReadUInt32();

            UpdateMogg = ReadUpdateInfo(reader);
            UpdateMilo = ReadUpdateInfo(reader);
            UpdateImage = ReadUpdateInfo(reader);

            RealGuitarTuning = ReadIntArray(reader);
            RealBassTuning = ReadIntArray(reader);

            DrumIndices = ReadIntArray(reader);
            BassIndices = ReadIntArray(reader);
            GuitarIndices = ReadIntArray(reader);
            KeysIndices = ReadIntArray(reader);
            VocalsIndices = ReadIntArray(reader);
            TrackIndices = ReadIntArray(reader);
            CrowdIndices = ReadIntArray(reader);

            DrumStemValues = ReadFloatArray(reader);
            BassStemValues = ReadFloatArray(reader);
            GuitarStemValues = ReadFloatArray(reader);
            KeysStemValues = ReadFloatArray(reader);
            VocalsStemValues = ReadFloatArray(reader);
            TrackStemValues = ReadFloatArray(reader);
            CrowdStemValues = ReadFloatArray(reader);
        }

        protected DTAResult Init(string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, string defaultPlaylist)
        {
            var dtaResults = ParseDTA(nodeName, reader);
            ApplyRBCONUpdates(nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);
            FinalizeRBCONAudioValues(dtaResults.pans, dtaResults.volumes, dtaResults.cores);

            if (Metadata.Playlist.Length == 0)
                Metadata.Playlist = defaultPlaylist;
            return dtaResults;
        }

        protected ScanResult ParseRBCONMidi(CONFile? file)
        {
            if (Metadata.Name.Length == 0)
            {
                return ScanResult.NoName;
            }

            if (!IsMoggValid(file))
            {
                return ScanResult.MoggError;
            }

            try
            {
                byte[]? chartFile = LoadMidiFile(file);
                byte[]? updateFile = LoadUpdateMidiFile();
                byte[]? upgradeFile = Upgrade?.LoadUpgradeMidi();

                DrumPreparseHandler drumTracker = new()
                {
                    Type = DrumsType.ProDrums
                };

                int bufLength = 0;
                if (UpdateMidi != null)
                {
                    if (updateFile == null)
                        return ScanResult.MissingUpdateMidi;

                    if (!Metadata.Parts.ParseMidi(updateFile, drumTracker))
                        return ScanResult.MultipleMidiTrackNames_Update;

                    bufLength += updateFile.Length;
                }

                if (Upgrade != null)
                {
                    if (upgradeFile == null)
                        return ScanResult.MissingUpgradeMidi;

                    if (!Metadata.Parts.ParseMidi(upgradeFile, drumTracker))
                        return ScanResult.MultipleMidiTrackNames_Upgrade;

                    bufLength += upgradeFile.Length;
                }

                if (chartFile == null)
                    return ScanResult.MissingMidi;

                if (!Metadata.Parts.ParseMidi(chartFile, drumTracker))
                    return ScanResult.MultipleMidiTrackNames;

                bufLength += chartFile.Length;

                Metadata.Parts.SetDrums(drumTracker);
                if (!Metadata.Parts.CheckScanValidity())
                    return ScanResult.NoNotes;

                byte[] buffer = new byte[bufLength];
                System.Runtime.CompilerServices.Unsafe.CopyBlock(ref buffer[0], ref chartFile[0], (uint) chartFile.Length);

                int offset = chartFile.Length;
                if (updateFile != null)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(ref buffer[offset], ref updateFile[0], (uint) updateFile.Length);
                    offset += updateFile.Length;
                }

                if (upgradeFile != null)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(ref buffer[offset], ref upgradeFile[0], (uint) upgradeFile.Length);
                }
                Metadata.Hash = HashWrapper.Hash(buffer);
                return ScanResult.Success;
            }
            catch
            {
                return ScanResult.PossibleCorruption;
            }
        }

        private static AbridgedFileInfo? ReadUpdateInfo(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }
            return new AbridgedFileInfo(reader.ReadString(), false);
        }

        private static int[] ReadIntArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<int>();

            int[] values = new int[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadInt32();
            return values;
        }

        private static float[] ReadFloatArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<float>();

            float[] values = new float[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadSingle();
            return values;
        }

        private static void WriteUpdateInfo(AbridgedFileInfo? info, BinaryWriter writer)
        {
            if (info != null)
            {
                writer.Write(true);
                writer.Write(info.FullName);
            }
            else
                writer.Write(false);
        }

        private static void WriteArray(int[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        private static void WriteArray(float[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        private byte[]? LoadUpdateMidiFile()
        {
            if (UpdateMidi == null || !UpdateMidi.IsStillValid(false))
            {
                return null;
            }
            return File.ReadAllBytes(UpdateMidi.FullName);
        }

        private void ApplyRBCONUpdates(string nodeName, Dictionary<string, List<SongUpdate>> updates)
        {
            if (updates.TryGetValue(nodeName, out var updateList))
            {
                foreach (var update in updateList!)
                {
                    try
                    {
                        var updateResults = ParseDTA(nodeName, update.Readers);
                        if (update.Files != null)
                        {
                            Update(update.Files, updateResults);
                        }
                        else if (updateResults.discUpdate)
                        {
                            YargTrace.LogWarning($"Update midi expected with {update.Directory} - {nodeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        YargTrace.LogException(ex, $"Error processing CON Update {update.Directory} - {nodeName}!");
                    }
                }
            }
        }

        private void ApplyRBProUpgrade(string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                try
                {
                    ParseDTA(nodeName, upgrade.Item1!.Clone());
                    Upgrade = upgrade.Item2;
                }
                catch (Exception ex)
                {
                    YargTrace.LogException(ex, $"Error processing CON Upgrade {nodeName}!");
                }
            }
        }

        private DTAResult ParseDTA(string nodeName, params YARGDTAReader[] readers)
        {
            DTAResult result = new(nodeName);
            foreach (var reader in readers)
            {
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    switch (name)
                    {
                        case "name": Metadata.Name = reader.ExtractText(); break;
                        case "artist": Metadata.Artist = reader.ExtractText(); break;
                        case "master": Metadata.IsMaster = reader.ExtractBoolean(); break;
                        case "context": /*Context = reader.Read<uint>();*/ break;
                        case "song": SongLoop(result, reader); break;
                        case "song_vocals": while (reader.StartNode()) reader.EndNode(); break;
                        case "song_scroll_speed": VocalSongScrollSpeed = reader.ExtractUInt32(); break;
                        case "tuning_offset_cents": TuningOffsetCents = reader.ExtractInt32(); break;
                        case "bank": VocalPercussionBank = reader.ExtractText(); break;
                        case "anim_tempo":
                            {
                                string val = reader.ExtractText();
                                AnimTempo = val switch
                                {
                                    "kTempoSlow" => 16,
                                    "kTempoMedium" => 32,
                                    "kTempoFast" => 64,
                                    _ => uint.Parse(val)
                                };
                                break;
                            }
                        case "preview":
                            Metadata.PreviewStart = reader.ExtractUInt64();
                            Metadata.PreviewEnd = reader.ExtractUInt64();
                            break;
                        case "rank": Metadata.Parts.SetIntensities(RBDifficulties, reader); break;
                        case "solo": Soloes = reader.ExtractList_String().ToArray(); break;
                        case "genre": Metadata.Genre = reader.ExtractText(); break;
                        case "decade": /*Decade = reader.ExtractText();*/ break;
                        case "vocal_gender": VocalGender = reader.ExtractText() == "male"; break;
                        case "format": /*Format = reader.Read<uint>();*/ break;
                        case "version": VenueVersion = reader.ExtractUInt32(); break;
                        case "fake": /*IsFake = reader.ExtractText();*/ break;
                        case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                        case "game_origin":
                            {
                                string str = reader.ExtractText();
                                if ((str == "ugc" || str == "ugc_plus"))
                                {
                                    if (!nodeName.StartsWith("UGC_"))
                                        Metadata.Source = "customs";
                                }
                                else
                                    Metadata.Source = str;

                                //// if the source is any official RB game or its DLC, charter = Harmonix
                                //if (SongSources.GetSource(str).Type == SongSources.SourceType.RB)
                                //{
                                //    _charter = "Harmonix";
                                //}

                                //// if the source is meant for usage in TBRB, it's a master track
                                //// TODO: NEVER assume localized version contains "Beatles"
                                //if (SongSources.SourceToGameName(str).Contains("Beatles")) _isMaster = true;
                                break;
                            }
                        case "song_id": SongID = reader.ExtractText(); break;
                        case "rating": SongRating = reader.ExtractUInt32(); break;
                        case "short_version": /*ShortVersion = reader.Read<uint>();*/ break;
                        case "album_art": HasAlbumArt = reader.ExtractBoolean(); break;
                        case "year_released":
                        case "year_recorded": YearAsNumber = reader.ExtractInt32(); break;
                        case "album_name": Metadata.Album = reader.ExtractText(); break;
                        case "album_track_number": Metadata.AlbumTrack = reader.ExtractUInt16(); break;
                        case "pack_name": Metadata.Playlist = reader.ExtractText(); break;
                        case "base_points": /*BasePoints = reader.Read<uint>();*/ break;
                        case "band_fail_cue": /*BandFailCue = reader.ExtractText();*/ break;
                        case "drum_bank": DrumBank = reader.ExtractText(); break;
                        case "song_length": Metadata.SongLength = reader.ExtractUInt64(); break;
                        case "sub_genre": /*Subgenre = reader.ExtractText();*/ break;
                        case "author": Metadata.Charter = reader.ExtractText(); break;
                        case "guide_pitch_volume": /*GuidePitchVolume = reader.ReadFloat();*/ break;
                        case "encoding":
                            var encoding = reader.ExtractText().ToLower() switch
                            {
                                "latin1" => YARGTextContainer.Latin1,
                                "utf-8" or
                                "utf8" => Encoding.UTF8,
                                _ => reader.encoding
                            };

                            if (reader.encoding != encoding)
                            {
                                string Convert(string str)
                                {
                                    byte[] bytes = reader.encoding.GetBytes(str);
                                    return encoding.GetString(bytes);
                                }

                                if (Metadata.Name != SongMetadata.DEFAULT_NAME)
                                    Metadata.Name = Convert(Metadata.Name);

                                if (Metadata.Artist != SongMetadata.DEFAULT_ARTIST)
                                    Metadata.Artist = Convert(Metadata.Artist);

                                if (Metadata.Album != SongMetadata.DEFAULT_ALBUM)
                                    Metadata.Album = Convert(Metadata.Album);

                                if (Metadata.Genre != SongMetadata.DEFAULT_GENRE)
                                    Metadata.Genre = Convert(Metadata.Genre);

                                if (Metadata.Charter != SongMetadata.DEFAULT_CHARTER)
                                    Metadata.Charter = Convert(Metadata.Charter);

                                if (Metadata.Source != SongMetadata.DEFAULT_SOURCE)
                                    Metadata.Source = Convert(Metadata.Source);

                                if (Metadata.Playlist.Str.Length != 0)
                                    Metadata.Playlist = Convert(Metadata.Playlist);
                                reader.encoding = encoding;
                            }

                            break;
                        case "vocal_tonic_note": VocalTonicNote = reader.ExtractUInt32(); break;
                        case "song_tonality": SongTonality = reader.ExtractBoolean(); break;
                        case "alternate_path": result.alternatePath = reader.ExtractBoolean(); break;
                        case "real_guitar_tuning":
                            {
                                if (reader.StartNode())
                                {
                                    RealGuitarTuning = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    RealGuitarTuning = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "real_bass_tuning":
                            {
                                if (reader.StartNode())
                                {
                                    RealBassTuning = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    RealBassTuning = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "video_venues":
                            {
                                if (reader.StartNode())
                                {
                                    VideoVenues = reader.ExtractList_String().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    VideoVenues = new[] { reader.ExtractText() };
                                break;
                            }
                        case "extra_authoring":
                            {
                                StringBuilder authors = new();
                                foreach (string str in reader.ExtractList_String())
                                {
                                    if (str == "disc_update")
                                        result.discUpdate = true;
                                    else if (authors.Length == 0 && Metadata.Charter == SongMetadata.DEFAULT_CHARTER)
                                        authors.Append(str);
                                    else
                                    {
                                        if (authors.Length == 0)
                                            authors.Append(Metadata.Charter);
                                        authors.Append(", " + str);
                                    }
                                }

                                if (authors.Length == 0)
                                    authors.Append(Metadata.Charter);

                                Metadata.Charter = authors.ToString();
                            }
                            break;
                    }
                    reader.EndNode();
                }
            }
            return result;
        }

        private void SongLoop(DTAResult result, YARGDTAReader reader)
        {
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": result.location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(reader); break;
                    case "crowd_channels": CrowdIndices = reader.ExtractList_Int().ToArray(); break;
                    //case "vocal_parts": VocalParts = reader.Read<ushort>(); break;
                    case "pans":
                        if (reader.StartNode())
                        {
                            result.pans = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            result.pans = new[] { reader.ExtractFloat() };
                        break;
                    case "vols":
                        if (reader.StartNode())
                        {
                            result.volumes = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            result.volumes = new[] { reader.ExtractFloat() };
                        break;
                    case "cores":
                        if (reader.StartNode())
                        {
                            result.cores = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            result.cores = new[] { reader.ExtractFloat() };
                        break;
                    case "hopo_threshold": Metadata.ParseSettings.HopoThreshold = reader.ExtractInt64(); break;
                }
                reader.EndNode();
            }
        }

        private void TracksLoop(YARGDTAReader reader)
        {
            while (reader.StartNode())
            {
                while (reader.StartNode())
                {
                    switch (reader.GetNameOfNode())
                    {
                        case "drum":
                            {
                                if (reader.StartNode())
                                {
                                    DrumIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    DrumIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    BassIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    BassIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    GuitarIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    GuitarIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    KeysIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    KeysIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    VocalsIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    VocalsIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private void Update(SongUpdateFiles update, DTAResult results)
        {
            if (results.discUpdate)
            {
                if (update.Midi != null)
                {
                    if (UpdateMidi == null || update.Midi.LastUpdatedTime > UpdateMidi.LastUpdatedTime)
                    {
                        UpdateMidi = update.Midi;
                    }
                }
                else
                {
                    YargTrace.LogWarning($"Update midi expected in directory {update.Directory}");
                }
            }

            if (update.Mogg != null)
            {
                if (UpdateMogg == null || update.Mogg.LastUpdatedTime > UpdateMogg.LastUpdatedTime)
                {
                    UpdateMogg = update.Mogg;
                }
            }

            if (update.Milo != null)
            {
                if (UpdateMilo == null || update.Milo.LastUpdatedTime > UpdateMilo.LastUpdatedTime)
                {
                    UpdateMilo = update.Milo;
                }
            }

            if (HasAlbumArt && results.alternatePath)
            {
                if (update.Image != null)
                {
                    if (UpdateImage == null || update.Image.LastUpdatedTime > UpdateImage.LastUpdatedTime)
                    {
                        UpdateImage = update.Image;
                    }
                }
            }
        }

        private void FinalizeRBCONAudioValues(float[] pans, float[] volumes, float[] cores)
        {
            HashSet<int> pending = new();
            for (int i = 0; i < pans.Length; i++)
                pending.Add(i);

            if (DrumIndices != Array.Empty<int>())
                DrumStemValues = CalculateStemValues(DrumIndices);

            if (BassIndices != Array.Empty<int>())
                BassStemValues = CalculateStemValues(BassIndices);

            if (GuitarIndices != Array.Empty<int>())
                GuitarStemValues = CalculateStemValues(GuitarIndices);

            if (KeysIndices != Array.Empty<int>())
                KeysStemValues = CalculateStemValues(KeysIndices);

            if (VocalsIndices != Array.Empty<int>())
                VocalsStemValues = CalculateStemValues(VocalsIndices);

            if (CrowdIndices != Array.Empty<int>())
                CrowdStemValues = CalculateStemValues(CrowdIndices);

            TrackIndices = pending.ToArray();
            TrackStemValues = CalculateStemValues(TrackIndices);

            float[] CalculateStemValues(int[] indices)
            {
                float[] values = new float[2 * indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    int index = indices[i];
                    float theta = (pans[index] + 1) * ((float) Math.PI / 4);
                    float volRatio = (float) Math.Pow(10, volumes[index] / 20);
                    values[2 * i] = volRatio * (float) Math.Cos(theta);
                    values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                    pending.Remove(index);
                }
                return values;
            }
        }

        private int GetMoggVersion()
        {
            using var stream = GetMoggStream();
            return stream?.Read<int>(Endianness.Little) ?? 0;
        }

        private Func<SongStem, int[], float[], AudioChannel?>? InitMoggFunc()
        {
            var stream = GetMoggStream();
            if (stream == null)
            {
                YargTrace.LogError("Unknown error while loading Mogg");
                return null;
            }

            using var wrapper = DisposableCounter.Wrap(stream);
            if (stream.Read<int>(Endianness.Little) != 0x0A)
            {
                YargTrace.LogError("Mogg version changed somehow");
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            wrapper.Release();

            return (SongStem stem, int[] indices, float[] panning) =>
            {
                Stream newStream = stream switch
                {
                    CONFileStream constream => constream.Clone(),
                    FileStream fileStream => new FileStream(fileStream.Name, FileMode.Open, FileAccess.Read, FileShare.Read, 1),
                    _ => throw new Exception()
                };
                newStream.Seek(start, SeekOrigin.Begin);
                return new AudioChannel(stem, newStream, indices, panning);
            };
        }

        private Func<SongStem, int[], float[], AudioChannel?>? InitYARGMoggFunc()
        {
            using var stream = GetMoggStream();
            if (stream == null || stream.Read<int>(Endianness.Little) != 0xF0)
            {
                YargTrace.LogError("Unknown error while loading YARG mogg");
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            var file = stream.ReadBytes((int) (stream.Length - start));
            return (SongStem stem, int[] indices, float[] panning) =>
            {
                var memStream = new MemoryStream(file);
                return new AudioChannel(stem, memStream, indices, panning);
            };
        }
    }
}
