using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Preparsers;

#nullable enable
namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        public sealed class RBCONSubMetadata
        {
            public static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
            public string Directory = string.Empty;

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

            public Encoding MidiEncoding = Latin1;

            public AbridgedFileInfo? UpdateMidi = null;
            public AbridgedFileInfo? Mogg = null;
            public AbridgedFileInfo? Milo = null;
            public AbridgedFileInfo? Image = null;

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

            public RBCONDifficulties RBDifficulties = new();

            public RBCONSubMetadata() { }

            public RBCONSubMetadata(YARGBinaryReader reader)
            {
                RBDifficulties = new(reader);
                Directory = reader.ReadLEBString();
                AnimTempo = reader.ReadUInt32();
                SongID = reader.ReadLEBString();
                VocalPercussionBank = reader.ReadLEBString();
                VocalSongScrollSpeed = reader.ReadUInt32();
                SongRating = reader.ReadUInt32();
                VocalGender = reader.ReadBoolean();
                VocalTonicNote = reader.ReadUInt32();
                SongTonality = reader.ReadBoolean();
                TuningOffsetCents = reader.ReadInt32();
                VenueVersion = reader.ReadUInt32();

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

            public void Serialize(BinaryWriter writer)
            {
                RBDifficulties.Serialize(writer);
                writer.Write(Directory);
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

            private static int[] ReadIntArray(YARGBinaryReader reader)
            {
                int length = reader.ReadInt32();
                if (length == 0)
                    return Array.Empty<int>();

                int[] values = new int[length];
                for (int i = 0; i < length; ++i)
                    values[i] = reader.ReadInt32();
                return values;
            }

            private static float[] ReadFloatArray(YARGBinaryReader reader)
            {
                int length = reader.ReadInt32();
                if (length == 0)
                    return Array.Empty<float>();

                float[] values = new float[length];
                for (int i = 0; i < length; ++i)
                    values[i] = reader.ReadFloat();
                return values;
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

            public void Update(string folder, string nodeName, DTAResult results)
            {
                string dir = Path.Combine(folder, nodeName);
                FileInfo info;
                if (results.discUpdate)
                {
                    string path = Path.Combine(dir, $"{nodeName}_update.mid");
                    info = new(path);
                    if (info.Exists)
                    {
                        if (UpdateMidi == null || UpdateMidi.LastWriteTime < info.LastWriteTime)
                            UpdateMidi = info;
                    }
                    else
                        UpdateMidi ??= info;
                }

                info = new(Path.Combine(dir, $"{nodeName}_update.mogg"));
                if (info.Exists && (Mogg == null || Mogg.LastWriteTime < info.LastWriteTime))
                    Mogg = info;
                dir = Path.Combine(dir, "gen");

                info = new(Path.Combine(dir, $"{nodeName}.milo_xbox"));
                if (info.Exists && (Milo == null || Milo.LastWriteTime < info.LastWriteTime))
                    Milo = info;

                if (HasAlbumArt && results.alternatePath)
                {
                    info = new(Path.Combine(dir, $"{nodeName}_keep.png_xbox"));
                    if (info.Exists && (Image == null || Image.LastWriteTime < info.LastWriteTime))
                        Image = info;
                }
            }

            public byte[]? LoadMidiUpdateFile()
            {
                if (UpdateMidi == null)
                    return null;

                FileInfo info = new(UpdateMidi.FullName);
                if (!info.Exists || info.LastWriteTime != UpdateMidi.LastWriteTime)
                    return null;
                return File.ReadAllBytes(UpdateMidi.FullName);
            }

            public Stream? GetMoggStream()
            {
                if (Mogg == null || !File.Exists(Mogg.FullName))
                    return null;

                if (Mogg.FullName.EndsWith(".yarg_mogg"))
                    return new YargMoggReadStream(Mogg.FullName);
                return new FileStream(Mogg.FullName, FileMode.Open, FileAccess.Read);
            }
        }

        public interface IRBCONMetadata
        {
            public RBCONSubMetadata SharedMetadata { get; }
            public DateTime MidiLastWrite { get; }
            public byte[]? LoadMidiFile();
            public byte[]? LoadMoggFile();
            public byte[]? LoadMiloFile();
            public byte[]? LoadImgFile();
            public bool IsMoggValid();
            public void Serialize(BinaryWriter writer);
        }

        public sealed class DTAResult
        {
            public readonly string nodeName;
            public bool alternatePath = false;
            public bool discUpdate = false;
            public string location = string.Empty;
            public DTAResult(string nodeName)
            {
                this.nodeName = nodeName;
            }
        }

        private SongMetadata(IRBCONMetadata rbMeta, YARGBinaryReader reader, CategoryCacheStrings strings) : this(reader, strings)
        {
            _rbData = rbMeta;
            _directory = rbMeta.SharedMetadata.Directory;
        }

        public ScanResult ParseRBCONMidi()
        {
            var sharedMetadata = _rbData!.SharedMetadata;
            if (_name.Length == 0)
            {
                return ScanResult.NoName;
            }

            if (!_rbData.IsMoggValid())
            {
                return ScanResult.MoggError;
            }

            try
            {
                byte[]? chartFile = _rbData.LoadMidiFile();
                byte[]? updateFile = sharedMetadata.LoadMidiUpdateFile();
                byte[]? upgradeFile = sharedMetadata.Upgrade?.LoadUpgradeMidi();

                int bufLength = 0;
                if (sharedMetadata.UpdateMidi != null)
                {
                    if (updateFile == null)
                        return ScanResult.MissingUpdateMidi;

                    _parts.ParseMidi(updateFile, DrumPreparseType.FourPro);
                    bufLength += updateFile.Length;
                }

                if (sharedMetadata.Upgrade != null)
                {
                    if (upgradeFile == null)
                        return ScanResult.MissingUpgradeMidi;

                    _parts.ParseMidi(upgradeFile, DrumPreparseType.FourPro);
                    bufLength += upgradeFile.Length;
                }

                if (chartFile == null)
                    return ScanResult.MissingMidi;

                _parts.ParseMidi(chartFile, DrumPreparseType.FourPro);
                bufLength += chartFile.Length;

                if (!_parts.CheckScanValidity())
                    return ScanResult.NoNotes;

                byte[] buffer = new byte[bufLength];
                unsafe
                {
                    fixed (byte* buf = buffer)
                    {
                        int offset = 0;
                        fixed (byte* chart = chartFile)
                        {
                            System.Runtime.CompilerServices.Unsafe.CopyBlock(buf, chart, (uint) chartFile.Length);
                            offset += chartFile.Length;
                        }

                        if (updateFile != null)
                        {
                            fixed (byte* update = updateFile)
                            {
                                System.Runtime.CompilerServices.Unsafe.CopyBlock(buf, update, (uint) updateFile.Length);
                                offset += updateFile.Length;
                            }
                        }

                        if (upgradeFile != null)
                        {
                            fixed (byte* upgrade = upgradeFile)
                            {
                                System.Runtime.CompilerServices.Unsafe.CopyBlock(buf, upgrade, (uint) upgradeFile.Length);
                                offset += upgradeFile.Length;
                            }
                        }
                    }
                }
                _hash = HashWrapper.Create(buffer);
                return ScanResult.Success;
            }
            catch
            {
                return ScanResult.PossibleCorruption;
            }
        }

        private void ApplyRBCONUpdates(string nodeName, Dictionary<string, List<(string, YARGDTAReader)>> updates)
        {
            var sharedMetadata = _rbData!.SharedMetadata;
            if (updates.TryGetValue(nodeName, out var updateList))
            {
                foreach (var update in updateList!)
                {
                    var updateResults = ParseDTA(nodeName, sharedMetadata, new YARGDTAReader(update.Item2));
                    sharedMetadata.Update(update.Item1, nodeName, updateResults);
                }
            }
        }

        private void ApplyRBProUpgrade(string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            var sharedMetadata = _rbData!.SharedMetadata;
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                sharedMetadata.Upgrade = upgrade.Item2;
                ParseDTA(nodeName, sharedMetadata, new YARGDTAReader(upgrade.Item1!));
            }
        }

        private DTAResult ParseDTA(string nodeName, RBCONSubMetadata rbConMetadata, YARGDTAReader reader)
        {
            DTAResult result = new(nodeName);
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                switch (name)
                {
                    case "name": _name = reader.ExtractText(); break;
                    case "artist": _artist = reader.ExtractText(); break;
                    case "master": _isMaster = reader.ReadBoolean(); break;
                    case "context": /*Context = reader.ReadUInt32();*/ break;
                    case "song": SongLoop(rbConMetadata, result, reader); break;
                    case "song_vocals": while (reader.StartNode()) reader.EndNode(); break;
                    case "song_scroll_speed": rbConMetadata.VocalSongScrollSpeed = reader.ReadUInt32(); break;
                    case "tuning_offset_cents": rbConMetadata.TuningOffsetCents = reader.ReadInt32(); break;
                    case "bank": rbConMetadata.VocalPercussionBank = reader.ExtractText(); break;
                    case "anim_tempo":
                        {
                            string val = reader.ExtractText();
                            rbConMetadata.AnimTempo = val switch
                            {
                                "kTempoSlow" => 16,
                                "kTempoMedium" => 32,
                                "kTempoFast" => 64,
                                _ => uint.Parse(val)
                            };
                            break;
                        }
                    case "preview":
                        _previewStart = reader.ReadUInt32();
                        _previewEnd = reader.ReadUInt32();
                        break;
                    case "rank": _parts.SetIntensities(rbConMetadata.RBDifficulties, reader); break;
                    case "solo": rbConMetadata.Soloes = reader.ExtractList_String().ToArray(); break;
                    case "genre": _genre = reader.ExtractText(); break;
                    case "decade": /*Decade = reader.ExtractText();*/ break;
                    case "vocal_gender": rbConMetadata.VocalGender = reader.ExtractText() == "male"; break;
                    case "format": /*Format = reader.ReadUInt32();*/ break;
                    case "version": rbConMetadata.VenueVersion = reader.ReadUInt32(); break;
                    case "fake": /*IsFake = reader.ExtractText();*/ break;
                    case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                    case "game_origin":
                        {
                            string str = reader.ExtractText();
                            if ((str == "ugc" || str == "ugc_plus"))
                            {
                                if (!nodeName.StartsWith("UGC_"))
                                    _source = "customs";
                            }
                            else
                                _source = str;

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
                    case "song_id": rbConMetadata.SongID = reader.ExtractText(); break;
                    case "rating": rbConMetadata.SongRating = reader.ReadUInt32(); break;
                    case "short_version": /*ShortVersion = reader.ReadUInt32();*/ break;
                    case "album_art": rbConMetadata.HasAlbumArt = reader.ReadBoolean(); break;
                    case "year_released":
                    case "year_recorded": YearAsNumber = reader.ReadInt32(); break;
                    case "album_name": _album = reader.ExtractText(); break;
                    case "album_track_number": _albumTrack = reader.ReadUInt16(); break;
                    case "pack_name": _playlist = reader.ExtractText(); break;
                    case "base_points": /*BasePoints = reader.ReadUInt32();*/ break;
                    case "band_fail_cue": /*BandFailCue = reader.ExtractText();*/ break;
                    case "drum_bank": rbConMetadata.DrumBank = reader.ExtractText(); break;
                    case "song_length": _songLength = reader.ReadUInt32(); break;
                    case "sub_genre": /*Subgenre = reader.ExtractText();*/ break;
                    case "author": _charter = reader.ExtractText(); break;
                    case "guide_pitch_volume": /*GuidePitchVolume = reader.ReadFloat();*/ break;
                    case "encoding":
                        rbConMetadata.MidiEncoding = reader.ExtractText() switch
                        {
                            "Latin1" => RBCONSubMetadata.Latin1,
                            "UTF8" => Encoding.UTF8,
                            _ => rbConMetadata.MidiEncoding
                        };
                        break;
                    case "vocal_tonic_note": rbConMetadata.VocalTonicNote = reader.ReadUInt32(); break;
                    case "song_tonality": rbConMetadata.SongTonality = reader.ReadBoolean(); break;
                    case "alternate_path": result.alternatePath = reader.ReadBoolean(); break;
                    case "real_guitar_tuning":
                        {
                            if (reader.StartNode())
                            {
                                rbConMetadata.RealGuitarTuning = reader.ExtractList_Int().ToArray();
                                reader.EndNode();
                            }
                            else
                                rbConMetadata.RealGuitarTuning = new[] { reader.ReadInt32() };
                            break;
                        }
                    case "real_bass_tuning":
                        {
                            if (reader.StartNode())
                            {
                                rbConMetadata.RealBassTuning = reader.ExtractList_Int().ToArray();
                                reader.EndNode();
                            }
                            else
                                rbConMetadata.RealBassTuning = new[] { reader.ReadInt32() };
                            break;
                        }
                    case "video_venues":
                        {
                            if (reader.StartNode())
                            {
                                rbConMetadata.VideoVenues = reader.ExtractList_String().ToArray();
                                reader.EndNode();
                            }
                            else
                                rbConMetadata.VideoVenues = new[] { reader.ExtractText() };
                            break;
                        }
                    case "extra_authoring":
                        {
                            StringBuilder authors = new();
                            foreach (string str in reader.ExtractList_String())
                            {
                                if (str == "disc_update")
                                    result.discUpdate = true;
                                else if (authors.Length == 0 && _charter == DEFAULT_CHARTER)
                                    authors.Append(str);
                                else
                                {
                                    if (authors.Length == 0)
                                        authors.Append(_charter);
                                    authors.Append(", " + str);
                                }
                            }

                            if (authors.Length == 0)
                                authors.Append(_charter);

                            _charter = authors.ToString();
                        }
                        break;
                }
                reader.EndNode();
            }

            return result;
        }

        private void SongLoop(RBCONSubMetadata rbConMetadata, DTAResult result, YARGDTAReader reader)
        {
            float[]? pan = null;
            float[]? volume = null;
            float[]? core = null;
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": result.location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(rbConMetadata, reader); break;
                    case "crowd_channels": rbConMetadata.CrowdIndices = reader.ExtractList_Int().ToArray(); break;
                    //case "vocal_parts": rbConMetadata.VocalParts = reader.ReadUInt16(); break;
                    case "pans":
                        if (reader.StartNode())
                        {
                            pan = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            pan = new[] { reader.ReadFloat() };
                        break;
                    case "vols":
                        if (reader.StartNode())
                        {
                            volume = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            volume = new[] { reader.ReadFloat() };
                        break;
                    case "cores":
                        if (reader.StartNode())
                        {
                            core = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            core = new[] { reader.ReadFloat() };
                        break;
                    case "hopo_threshold": _parseSettings.HopoThreshold = reader.ReadInt64(); break;
                }
                reader.EndNode();
            }

            if (pan != null && volume != null)
            {
                HashSet<int> pending = new();
                for (int i = 0; i < pan.Length; i++)
                    pending.Add(i);

                if (rbConMetadata.DrumIndices != Array.Empty<int>())
                    rbConMetadata.DrumStemValues = CalculateStemValues(rbConMetadata.DrumIndices, pan, volume, pending);

                if (rbConMetadata.BassIndices != Array.Empty<int>())
                    rbConMetadata.BassStemValues = CalculateStemValues(rbConMetadata.BassIndices, pan, volume, pending);

                if (rbConMetadata.GuitarIndices != Array.Empty<int>())
                    rbConMetadata.GuitarStemValues = CalculateStemValues(rbConMetadata.GuitarIndices, pan, volume, pending);

                if (rbConMetadata.KeysIndices != Array.Empty<int>())
                    rbConMetadata.KeysStemValues = CalculateStemValues(rbConMetadata.KeysIndices, pan, volume, pending);

                if (rbConMetadata.VocalsIndices != Array.Empty<int>())
                    rbConMetadata.VocalsStemValues = CalculateStemValues(rbConMetadata.VocalsIndices, pan, volume, pending);

                if (rbConMetadata.CrowdIndices != Array.Empty<int>())
                    rbConMetadata.CrowdStemValues = CalculateStemValues(rbConMetadata.CrowdIndices, pan, volume, pending);

                rbConMetadata.TrackIndices = pending.ToArray();
                rbConMetadata.TrackStemValues = CalculateStemValues(rbConMetadata.TrackIndices, pan, volume, pending);
            }
        }

        private static void TracksLoop(RBCONSubMetadata rbConMetadata, YARGDTAReader reader)
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
                                    rbConMetadata.DrumIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    rbConMetadata.DrumIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    rbConMetadata.BassIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    rbConMetadata.BassIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    rbConMetadata.GuitarIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    rbConMetadata.GuitarIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    rbConMetadata.KeysIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    rbConMetadata.KeysIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    rbConMetadata.VocalsIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    rbConMetadata.VocalsIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private static float[] CalculateStemValues(int[] indices, float[] pan, float[] volume, HashSet<int> pending)
        {
            float[] values = new float[2 * indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                float theta = (pan[index] + 1) * ((float) Math.PI / 4);
                float volRatio = (float) Math.Pow(10, volume[index] / 20);
                values[2 * i] = volRatio * (float) Math.Cos(theta);
                values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                pending.Remove(index);
            }
            return values;
        }

        private SongChart LoadCONChart()
        {
            // Read base MIDI
            byte[]? chartFile = RBData!.LoadMidiFile();
            if (chartFile == null)
                throw new Exception("Base MIDI file not present");

            MidiFile midi;
            using (MemoryStream chartStream = new(chartFile))
            {
                midi = MidiFile.Read(chartStream);
            }

            // Merge update MIDI
            var shared = RBData.SharedMetadata;
            if (shared.UpdateMidi != null)
            {
                chartFile = shared.LoadMidiUpdateFile();
                if (chartFile == null)
                    throw new Exception("Scanned update MIDI file not present");

                using MemoryStream updateStream = new(chartFile);
                var update = MidiFile.Read(updateStream);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (shared.Upgrade != null)
            {
                chartFile = shared.Upgrade.LoadUpgradeMidi();
                if (chartFile == null)
                    throw new Exception("Scanned upgrade MIDI file not present");

                using MemoryStream upgradeStream = new(chartFile);
                var upgrade = MidiFile.Read(upgradeStream);
                midi.Merge(upgrade);
            }

            return SongChart.FromMidi(_parseSettings, midi);
        }
    }
}
