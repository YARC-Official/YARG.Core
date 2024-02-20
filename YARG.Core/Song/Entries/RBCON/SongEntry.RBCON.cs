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

namespace YARG.Core.Song
{
    public struct RBMetadata
    {
        public static readonly RBMetadata Default = new()
        {
            SongID = string.Empty,
            DrumBank = string.Empty,
            VocalPercussionBank = string.Empty,
            VocalGender = true
        };

        public string SongID;
        public uint AnimTempo;
        public string DrumBank;
        public string VocalPercussionBank;
        public uint VocalSongScrollSpeed;
        public bool VocalGender; //true for male, false for female
        //public bool HasAlbumArt;
        //public bool IsFake;
        public uint VocalTonicNote;
        public bool SongTonality; // 0 = major, 1 = minor
        public int TuningOffsetCents;
        public uint VenueVersion;

        public string[]? Soloes;
        public string[]? VideoVenues;

        public int[]? RealGuitarTuning;
        public int[]? RealBassTuning;

        public int[]? DrumIndices;
        public int[]? BassIndices;
        public int[]? GuitarIndices;
        public int[]? KeysIndices;
        public int[]? VocalsIndices;
        public int[]? CrowdIndices;
        public int[]? TrackIndices;

        public float[]? TrackStemValues;
        public float[]? DrumStemValues;
        public float[]? BassStemValues;
        public float[]? GuitarStemValues;
        public float[]? KeysStemValues;
        public float[]? VocalsStemValues;
        public float[]? CrowdStemValues;
    }

    public abstract class RBCONEntry : SongEntry
    {
        protected struct DTAResult
        {
            public bool alternatePath;
            public bool discUpdate;
            public string location;
            public float[]? pans;
            public float[]? volumes;
            public float[]? cores;
        }

        private RBMetadata _rbMetadata;
        private RBCONDifficulties _rbDifficulties;

        private AbridgedFileInfo? _updateMidi;
        private IRBProUpgrade? _upgrade;

        private AbridgedFileInfo? UpdateMogg;
        private AbridgedFileInfo? UpdateMilo;
        private AbridgedFileInfo? UpdateImage;

        public int RBBandDiff => _rbDifficulties.Band;

        protected abstract DateTime MidiLastUpdate { get; }

        public override DateTime GetAddTime()
        {
            var lastUpdateTime = MidiLastUpdate;
            if (_updateMidi != null)
            {
                if (_updateMidi.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = _updateMidi.LastUpdatedTime;
                }
            }

            if (_upgrade != null)
            {
                if (_upgrade.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = _upgrade.LastUpdatedTime;
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
            if (_updateMidi != null)
            {
                if (!_updateMidi.IsStillValid(false))
                    return null;

                using var midiStream = new FileStream(_updateMidi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (_upgrade != null)
            {
                using var midiStream = _upgrade.GetUpgradeMidiStream();
                if (midiStream == null)
                    return null;
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            return SongChart.FromMidi(_metadata.ParseSettings, midi);
        }

        public override AudioMixer? LoadAudioStreams(params SongStem[] ignoreStems)
        {
            var stream = GetMoggStream();
            if (stream == null)
            {
                return null;
            }

            using var wrapper = DisposableCounter.Wrap(stream);
            int version = stream.Read<int>(Endianness.Little);
            if (version is not 0x0A and not 0xF0)
            {
                YargTrace.LogError("Original unencrypted mogg replaced by an encrypted mogg");
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            var mixer = new AudioMixer(wrapper.Release());
            if (_rbMetadata.DrumIndices != null && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (_rbMetadata.DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums, _rbMetadata.DrumIndices, _rbMetadata.DrumStemValues!));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums1, _rbMetadata.DrumIndices[0..1], _rbMetadata.DrumStemValues![0..2]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums2, _rbMetadata.DrumIndices[1..3], _rbMetadata.DrumStemValues[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums1, _rbMetadata.DrumIndices[0..1], _rbMetadata.DrumStemValues![0..2]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums2, _rbMetadata.DrumIndices[1..2], _rbMetadata.DrumStemValues[2..4]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums3, _rbMetadata.DrumIndices[2..4], _rbMetadata.DrumStemValues[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums1, _rbMetadata.DrumIndices[0..1], _rbMetadata.DrumStemValues![0..2]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums2, _rbMetadata.DrumIndices[1..3], _rbMetadata.DrumStemValues[2..6]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums3, _rbMetadata.DrumIndices[3..5], _rbMetadata.DrumStemValues[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums1, _rbMetadata.DrumIndices[0..2], _rbMetadata.DrumStemValues![0..4]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums2, _rbMetadata.DrumIndices[2..4], _rbMetadata.DrumStemValues[4..8]));
                        mixer.Channels.Add(new AudioChannel(SongStem.Drums3, _rbMetadata.DrumIndices[4..6], _rbMetadata.DrumStemValues[8..12]));
                        break;
                }
            }

            if (_rbMetadata.BassIndices != null && !ignoreStems.Contains(SongStem.Bass))
                mixer.Channels.Add(new AudioChannel(SongStem.Bass, _rbMetadata.BassIndices, _rbMetadata.BassStemValues!));

            if (_rbMetadata.GuitarIndices != null && !ignoreStems.Contains(SongStem.Guitar))
                mixer.Channels.Add(new AudioChannel(SongStem.Guitar, _rbMetadata.GuitarIndices, _rbMetadata.GuitarStemValues!));

            if (_rbMetadata.KeysIndices != null && !ignoreStems.Contains(SongStem.Keys))
                mixer.Channels.Add(new AudioChannel(SongStem.Keys, _rbMetadata.KeysIndices, _rbMetadata.KeysStemValues!));

            if (_rbMetadata.VocalsIndices != null && !ignoreStems.Contains(SongStem.Vocals))
                mixer.Channels.Add(new AudioChannel(SongStem.Vocals, _rbMetadata.VocalsIndices, _rbMetadata.VocalsStemValues!));

            if (_rbMetadata.TrackIndices != null && !ignoreStems.Contains(SongStem.Song))
                mixer.Channels.Add(new AudioChannel(SongStem.Song, _rbMetadata.TrackIndices, _rbMetadata.TrackStemValues!));

            if (_rbMetadata.CrowdIndices != null && !ignoreStems.Contains(SongStem.Crowd))
                mixer.Channels.Add(new AudioChannel(SongStem.Crowd, _rbMetadata.CrowdIndices, _rbMetadata.CrowdStemValues!));
            return mixer;
        }

        public override AudioMixer? LoadPreviewAudio()
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

        public virtual void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(_updateMidi != null);
            _updateMidi?.Serialize(writer);

            SerializeMetadata(writer, node);

            WriteUpdateInfo(UpdateMogg, writer);
            WriteUpdateInfo(UpdateMilo, writer);
            WriteUpdateInfo(UpdateImage, writer);

            writer.Write(_rbMetadata.AnimTempo);
            writer.Write(_rbMetadata.SongID);
            writer.Write(_rbMetadata.VocalPercussionBank);
            writer.Write(_rbMetadata.VocalSongScrollSpeed);
            writer.Write(_rbMetadata.VocalGender);
            writer.Write(_rbMetadata.VocalTonicNote);
            writer.Write(_rbMetadata.SongTonality);
            writer.Write(_rbMetadata.TuningOffsetCents);
            writer.Write(_rbMetadata.VenueVersion);
            writer.Write(_rbMetadata.DrumBank);

            WriteArray(_rbMetadata.RealGuitarTuning, writer);
            WriteArray(_rbMetadata.RealBassTuning, writer);

            WriteArray(_rbMetadata.DrumIndices, writer);
            WriteArray(_rbMetadata.BassIndices, writer);
            WriteArray(_rbMetadata.GuitarIndices, writer);
            WriteArray(_rbMetadata.KeysIndices, writer);
            WriteArray(_rbMetadata.VocalsIndices, writer);
            WriteArray(_rbMetadata.TrackIndices, writer);
            WriteArray(_rbMetadata.CrowdIndices, writer);

            WriteArray(_rbMetadata.DrumStemValues, writer);
            WriteArray(_rbMetadata.BassStemValues, writer);
            WriteArray(_rbMetadata.GuitarStemValues, writer);
            WriteArray(_rbMetadata.KeysStemValues, writer);
            WriteArray(_rbMetadata.VocalsStemValues, writer);
            WriteArray(_rbMetadata.TrackStemValues, writer);
            WriteArray(_rbMetadata.CrowdStemValues, writer);

            WriteStringArray(_rbMetadata.Soloes, writer);
            WriteStringArray(_rbMetadata.VideoVenues, writer);

            unsafe
            {
                fixed (RBCONDifficulties* ptr = &_rbDifficulties)
                {
                    var span = new ReadOnlySpan<byte>(ptr, sizeof(RBCONDifficulties));
                    writer.Write(span);
                }
            }    
        }

        protected abstract bool IsMoggValid(CONFile? file);
        protected abstract byte[]? LoadMidiFile(CONFile? file);
        protected abstract Stream? GetMidiStream();

        protected RBCONEntry() : base()
        {
            _rbMetadata = RBMetadata.Default;
            _rbDifficulties = RBCONDifficulties.Default;
        }

        protected RBCONEntry(AbridgedFileInfo? updateMidi, IRBProUpgrade? upgrade, BinaryReader reader, CategoryCacheStrings strings)
            : base(reader, strings)
        {
            _updateMidi = updateMidi;
            _upgrade = upgrade;

            UpdateMogg = ReadUpdateInfo(reader);
            UpdateMilo = ReadUpdateInfo(reader);
            UpdateImage = ReadUpdateInfo(reader);

            _rbMetadata.AnimTempo = reader.ReadUInt32();
            _rbMetadata.SongID = reader.ReadString();
            _rbMetadata.VocalPercussionBank = reader.ReadString();
            _rbMetadata.VocalSongScrollSpeed = reader.ReadUInt32();
            _rbMetadata.VocalGender = reader.ReadBoolean();
            _rbMetadata.VocalTonicNote = reader.ReadUInt32();
            _rbMetadata.SongTonality = reader.ReadBoolean();
            _rbMetadata.TuningOffsetCents = reader.ReadInt32();
            _rbMetadata.VenueVersion = reader.ReadUInt32();
            _rbMetadata.DrumBank = reader.ReadString();

            ReadArray(out _rbMetadata.RealGuitarTuning, reader);
            ReadArray(out _rbMetadata.RealBassTuning, reader);

            ReadArray(out _rbMetadata.DrumIndices, reader);
            ReadArray(out _rbMetadata.BassIndices, reader);
            ReadArray(out _rbMetadata.GuitarIndices, reader);
            ReadArray(out _rbMetadata.KeysIndices, reader);
            ReadArray(out _rbMetadata.VocalsIndices, reader);
            ReadArray(out _rbMetadata.TrackIndices, reader);
            ReadArray(out _rbMetadata.CrowdIndices, reader);

            ReadArray(out _rbMetadata.DrumStemValues, reader);
            ReadArray(out _rbMetadata.BassStemValues, reader);
            ReadArray(out _rbMetadata.GuitarStemValues, reader);
            ReadArray(out _rbMetadata.KeysStemValues, reader);
            ReadArray(out _rbMetadata.VocalsStemValues, reader);
            ReadArray(out _rbMetadata.TrackStemValues, reader);
            ReadArray(out _rbMetadata.CrowdStemValues, reader);

            _rbMetadata.Soloes = ReadStringArray(reader);
            _rbMetadata.VideoVenues = ReadStringArray(reader);

            unsafe
            {
                fixed (RBCONDifficulties* ptr = &_rbDifficulties)
                {
                    var span = new Span<byte>(ptr, sizeof(RBCONDifficulties));
                    reader.Read(span);
                }
            }
        }

        protected DTAResult Init(string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, string defaultPlaylist)
        {
            var dtaResults = ParseDTA(nodeName, reader);
            ApplyRBCONUpdates(ref dtaResults, nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);

            if (dtaResults.pans == null || dtaResults.volumes == null || dtaResults.cores == null)
            {
                throw new Exception("Panning & Volume mappings not set from DTA");
            }
            FinalizeRBCONAudioValues(dtaResults.pans, dtaResults.volumes, dtaResults.cores);

            if (_metadata.Playlist.Length == 0)
                _metadata.Playlist = defaultPlaylist;
            return dtaResults;
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

        protected byte[]? LoadUpdateMidiFile()
        {
            if (_updateMidi == null || !_updateMidi.IsStillValid(false))
            {
                return null;
            }
            return File.ReadAllBytes(_updateMidi.FullName);
        }

        protected ScanResult ParseRBCONMidi(CONFile? file)
        {
            if (_metadata.Name.Length == 0)
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
                byte[]? upgradeFile = _upgrade?.LoadUpgradeMidi();

                DrumPreparseHandler drumTracker = new()
                {
                    Type = DrumsType.ProDrums
                };

                int bufLength = 0;
                if (_updateMidi != null)
                {
                    if (updateFile == null)
                        return ScanResult.MissingUpdateMidi;

                    if (!ParseMidi(updateFile, drumTracker, ref _parts))
                        return ScanResult.MultipleMidiTrackNames_Update;

                    bufLength += updateFile.Length;
                }

                if (_upgrade != null)
                {
                    if (upgradeFile == null)
                        return ScanResult.MissingUpgradeMidi;

                    if (!ParseMidi(upgradeFile, drumTracker, ref _parts))
                        return ScanResult.MultipleMidiTrackNames_Upgrade;

                    bufLength += upgradeFile.Length;
                }

                if (chartFile == null)
                    return ScanResult.MissingMidi;

                if (!ParseMidi(chartFile, drumTracker, ref _parts))
                    return ScanResult.MultipleMidiTrackNames;

                bufLength += chartFile.Length;

                SetDrums(ref _parts, drumTracker);
                if (!CheckScanValidity(in _parts))
                {
                    return ScanResult.NoNotes;
                }

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
                _hash = HashWrapper.Hash(buffer);
                return ScanResult.Success;
            }
            catch
            {
                return ScanResult.PossibleCorruption;
            }
        }

        private void ApplyRBCONUpdates(ref DTAResult mainResult, string nodeName, Dictionary<string, List<SongUpdate>> updates)
        {
            if (updates.TryGetValue(nodeName, out var updateList))
            {
                foreach (var update in updateList!)
                {
                    try
                    {
                        var updateResults = ParseDTA(nodeName, update.Readers);
                        Update(update, updateResults);

                        if (updateResults.cores != null)
                        {
                            mainResult.cores = updateResults.cores;
                        }

                        if (updateResults.volumes != null)
                        {
                            mainResult.volumes = updateResults.volumes;
                        }

                        if (updateResults.pans != null)
                        {
                            mainResult.pans = updateResults.pans;
                        }
                    }
                    catch (Exception ex)
                    {
                        YargTrace.LogException(ex, $"Error processing CON Update {update.BaseDirectory} - {nodeName}!");
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
                    _upgrade = upgrade.Item2;
                }
                catch (Exception ex)
                {
                    YargTrace.LogException(ex, $"Error processing CON Upgrade {nodeName}!");
                }
            }
        }

        private DTAResult ParseDTA(string nodeName, params YARGDTAReader[] readers)
        {
            DTAResult result = default;
            foreach (var reader in readers)
            {
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    switch (name)
                    {
                        case "name": _metadata.Name = reader.ExtractText(); break;
                        case "artist": _metadata.Artist = reader.ExtractText(); break;
                        case "master": _metadata.IsMaster = reader.ExtractBoolean(); break;
                        case "context": /*Context = reader.Read<uint>();*/ break;
                        case "song": SongLoop(ref result, reader); break;
                        case "song_vocals": while (reader.StartNode()) reader.EndNode(); break;
                        case "song_scroll_speed": _rbMetadata.VocalSongScrollSpeed = reader.ExtractUInt32(); break;
                        case "tuning_offset_cents": _rbMetadata.TuningOffsetCents = reader.ExtractInt32(); break;
                        case "bank": _rbMetadata.VocalPercussionBank = reader.ExtractText(); break;
                        case "anim_tempo":
                            {
                                string val = reader.ExtractText();
                                _rbMetadata.AnimTempo = val switch
                                {
                                    "kTempoSlow" => 16,
                                    "kTempoMedium" => 32,
                                    "kTempoFast" => 64,
                                    _ => uint.Parse(val)
                                };
                                break;
                            }
                        case "preview":
                            _metadata.PreviewStart = reader.ExtractUInt64();
                            _metadata.PreviewEnd = reader.ExtractUInt64();
                            break;
                        case "rank": DifficultyLoop(reader); break;
                        case "solo": _rbMetadata.Soloes = reader.ExtractList_String().ToArray(); break;
                        case "genre": _metadata.Genre = reader.ExtractText(); break;
                        case "decade": /*Decade = reader.ExtractText();*/ break;
                        case "vocal_gender": _rbMetadata.VocalGender = reader.ExtractText() == "male"; break;
                        case "format": /*Format = reader.Read<uint>();*/ break;
                        case "version": _rbMetadata.VenueVersion = reader.ExtractUInt32(); break;
                        case "fake": /*IsFake = reader.ExtractText();*/ break;
                        case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                        case "game_origin":
                            {
                                _metadata.Source = reader.ExtractText();

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
                        case "song_id": _rbMetadata.SongID = reader.ExtractText(); break;
                        case "rating": _metadata.SongRating = reader.ExtractUInt32(); break;
                        case "short_version": /*ShortVersion = reader.Read<uint>();*/ break;
                        case "album_art": /*HasAlbumArt = reader.ExtractBoolean();*/ break;
                        case "year_released":
                        case "year_recorded": YearAsNumber = reader.ExtractInt32(); break;
                        case "album_name": _metadata.Album = reader.ExtractText(); break;
                        case "album_track_number": _metadata.AlbumTrack = reader.ExtractUInt16(); break;
                        case "pack_name": _metadata.Playlist = reader.ExtractText(); break;
                        case "base_points": /*BasePoints = reader.Read<uint>();*/ break;
                        case "band_fail_cue": /*BandFailCue = reader.ExtractText();*/ break;
                        case "drum_bank": _rbMetadata.DrumBank = reader.ExtractText(); break;
                        case "song_length": _metadata.SongLength = reader.ExtractUInt64(); break;
                        case "sub_genre": /*Subgenre = reader.ExtractText();*/ break;
                        case "author": _metadata.Charter = reader.ExtractText(); break;
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

                                if (_metadata.Name != SongMetadata.DEFAULT_NAME)
                                    _metadata.Name = Convert(_metadata.Name);

                                if (_metadata.Artist != SongMetadata.DEFAULT_ARTIST)
                                    _metadata.Artist = Convert(_metadata.Artist);

                                if (_metadata.Album != SongMetadata.DEFAULT_ALBUM)
                                    _metadata.Album = Convert(_metadata.Album);

                                if (_metadata.Genre != SongMetadata.DEFAULT_GENRE)
                                    _metadata.Genre = Convert(_metadata.Genre);

                                if (_metadata.Charter != SongMetadata.DEFAULT_CHARTER)
                                    _metadata.Charter = Convert(_metadata.Charter);

                                if (_metadata.Source != SongMetadata.DEFAULT_SOURCE)
                                    _metadata.Source = Convert(_metadata.Source);

                                if (_metadata.Playlist.Str.Length != 0)
                                    _metadata.Playlist = Convert(_metadata.Playlist);
                                reader.encoding = encoding;
                            }

                            break;
                        case "vocal_tonic_note": _rbMetadata.VocalTonicNote = reader.ExtractUInt32(); break;
                        case "song_tonality": _rbMetadata.SongTonality = reader.ExtractBoolean(); break;
                        case "alternate_path": result.alternatePath = reader.ExtractBoolean(); break;
                        case "real_guitar_tuning":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.RealGuitarTuning = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.RealGuitarTuning = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "real_bass_tuning":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.RealBassTuning = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.RealBassTuning = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "video_venues":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.VideoVenues = reader.ExtractList_String().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.VideoVenues = new[] { reader.ExtractText() };
                                break;
                            }
                        case "extra_authoring":
                            {
                                StringBuilder authors = new();
                                foreach (string str in reader.ExtractList_String())
                                {
                                    if (str == "disc_update")
                                        result.discUpdate = true;
                                    else if (authors.Length == 0 && _metadata.Charter == SongMetadata.DEFAULT_CHARTER)
                                        authors.Append(str);
                                    else
                                    {
                                        if (authors.Length == 0)
                                            authors.Append(_metadata.Charter);
                                        authors.Append(", " + str);
                                    }
                                }

                                if (authors.Length == 0)
                                    authors.Append(_metadata.Charter);

                                _metadata.Charter = authors.ToString();
                            }
                            break;
                    }
                    reader.EndNode();
                }
            }
            return result;
        }

        private void SongLoop(ref DTAResult result, YARGDTAReader reader)
        {
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": result.location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(reader); break;
                    case "crowd_channels": _rbMetadata.CrowdIndices = reader.ExtractList_Int().ToArray(); break;
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
                    case "hopo_threshold": _metadata.ParseSettings.HopoThreshold = reader.ExtractInt64(); break;
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
                                    _rbMetadata.DrumIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.DrumIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.BassIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.BassIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.GuitarIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.GuitarIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.KeysIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.KeysIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    _rbMetadata.VocalsIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    _rbMetadata.VocalsIndices = new[] { reader.ExtractInt32() };
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private static readonly int[] BandDiffMap = { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap = { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap = { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap = { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap = { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap = { 132, 175, 218, 279, 353, 427 };

        private void DifficultyLoop(YARGDTAReader reader)
        {
            int diff;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                diff = reader.ExtractInt32();
                switch (name)
                {
                    case "drum":
                    case "drums":
                        _rbDifficulties.FourLaneDrums = (short) diff;
                        SetRank(ref _parts.FourLaneDrums.Intensity, diff, DrumDiffMap);
                        if (_parts.ProDrums.Intensity == -1)
                        {
                            _parts.ProDrums.Intensity = _parts.FourLaneDrums.Intensity;
                        }
                        break;
                    case "guitar":
                        _rbDifficulties.FiveFretGuitar = (short) diff;
                        SetRank(ref _parts.FiveFretGuitar.Intensity, diff, GuitarDiffMap);
                        if (_parts.ProGuitar_17Fret.Intensity == -1)
                        {
                            _parts.ProGuitar_22Fret.Intensity = _parts.ProGuitar_17Fret.Intensity = _parts.FiveFretGuitar.Intensity;
                        }
                        break;
                    case "bass":
                        _rbDifficulties.FiveFretBass = (short) diff;
                        SetRank(ref _parts.FiveFretBass.Intensity, diff, BassDiffMap);
                        if (_parts.ProBass_17Fret.Intensity == -1)
                        {
                            _parts.ProBass_22Fret.Intensity = _parts.ProBass_17Fret.Intensity = _parts.FiveFretBass.Intensity;
                        }
                        break;
                    case "vocals":
                        _rbDifficulties.LeadVocals = (short) diff;
                        SetRank(ref _parts.LeadVocals.Intensity, diff, VocalsDiffMap);
                        if (_parts.HarmonyVocals.Intensity == -1)
                        {
                            _parts.HarmonyVocals.Intensity = _parts.LeadVocals.Intensity;
                        }
                        break;
                    case "keys":
                        _rbDifficulties.Keys = (short) diff;
                        SetRank(ref _parts.Keys.Intensity, diff, KeysDiffMap);
                        if (_parts.ProKeys.Intensity == -1)
                        {
                            _parts.ProKeys.Intensity = _parts.Keys.Intensity;
                        }
                        break;
                    case "realGuitar":
                    case "real_guitar":
                        _rbDifficulties.ProGuitar = (short) diff;
                        SetRank(ref _parts.ProGuitar_17Fret.Intensity, diff, RealGuitarDiffMap);
                        _parts.ProGuitar_22Fret.Intensity = _parts.ProGuitar_17Fret.Intensity;
                        if (_parts.FiveFretGuitar.Intensity == -1)
                        {
                            _parts.FiveFretGuitar.Intensity = _parts.ProGuitar_17Fret.Intensity;
                        }
                        break;
                    case "realBass":
                    case "real_bass":
                        _rbDifficulties.ProBass = (short) diff;
                        SetRank(ref _parts.ProBass_17Fret.Intensity, diff, RealBassDiffMap);
                        _parts.ProBass_22Fret.Intensity = _parts.ProBass_17Fret.Intensity;
                        if (_parts.FiveFretBass.Intensity == -1)
                        {
                            _parts.FiveFretBass.Intensity = _parts.ProBass_17Fret.Intensity;
                        }
                        break;
                    case "realKeys":
                    case "real_keys":
                        _rbDifficulties.ProKeys = (short) diff;
                        SetRank(ref _parts.ProKeys.Intensity, diff, RealKeysDiffMap);
                        if (_parts.Keys.Intensity == -1)
                        {
                            _parts.Keys.Intensity = _parts.ProKeys.Intensity;
                        }
                        break;
                    case "realDrums":
                    case "real_drums":
                        _rbDifficulties.ProDrums = (short) diff;
                        SetRank(ref _parts.ProDrums.Intensity, diff, RealDrumsDiffMap);
                        if (_parts.FourLaneDrums.Intensity == -1)
                        {
                            _parts.FourLaneDrums.Intensity = _parts.ProDrums.Intensity;
                        }
                        break;
                    case "harmVocals":
                    case "vocal_harm":
                        _rbDifficulties.HarmonyVocals = (short) diff;
                        SetRank(ref _parts.HarmonyVocals.Intensity, diff, HarmonyDiffMap);
                        if (_parts.LeadVocals.Intensity == -1)
                        {
                            _parts.LeadVocals.Intensity = _parts.HarmonyVocals.Intensity;
                        }
                        break;
                    case "band":
                        _rbDifficulties.Band = (short) diff;
                        SetRank(ref _parts.BandDifficulty.Intensity, diff, BandDiffMap);
                        _parts.BandDifficulty.SubTracks = 1;
                        break;
                }
                reader.EndNode();
            }
        }

        private static void SetRank(ref sbyte intensity, int rank, int[] values)
        {
            sbyte i = 0;
            while (i < 6 && values[i] <= rank)
                ++i;
            intensity = i;
        }

        private void Update(SongUpdate update, in DTAResult results)
        {
            if (results.discUpdate)
            {
                if (update.Midi != null)
                {
                    if (_updateMidi == null || update.Midi.LastUpdatedTime > _updateMidi.LastUpdatedTime)
                    {
                        _updateMidi = update.Midi;
                    }
                }
                else
                {
                    YargTrace.LogWarning($"Update midi expected in directory {update.UpdateDirectory}");
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

            if (results.alternatePath)
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

            if (_rbMetadata.DrumIndices != null)
                _rbMetadata.DrumStemValues = CalculateStemValues(_rbMetadata.DrumIndices);

            if (_rbMetadata.BassIndices != null)
                _rbMetadata.BassStemValues = CalculateStemValues(_rbMetadata.BassIndices);

            if (_rbMetadata.GuitarIndices != null)
                _rbMetadata.GuitarStemValues = CalculateStemValues(_rbMetadata.GuitarIndices);

            if (_rbMetadata.KeysIndices != null)
                _rbMetadata.KeysStemValues = CalculateStemValues(_rbMetadata.KeysIndices);

            if (_rbMetadata.VocalsIndices != null)
                _rbMetadata.VocalsStemValues = CalculateStemValues(_rbMetadata.VocalsIndices);

            if (_rbMetadata.CrowdIndices != null)
                _rbMetadata.CrowdStemValues = CalculateStemValues(_rbMetadata.CrowdIndices);

            _rbMetadata.TrackIndices = pending.ToArray();
            _rbMetadata.TrackStemValues = CalculateStemValues(_rbMetadata.TrackIndices);

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

        private static AbridgedFileInfo? ReadUpdateInfo(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }
            return new AbridgedFileInfo(reader.ReadString(), false);
        }

        private static void ReadArray<T>(out T[]? values, BinaryReader reader)
            where T : unmanaged
        {
            int length = reader.ReadInt32();
            if (length == 0)
            {
                values = null;
                return;
            }

            values = new T[length];
            unsafe
            {
                fixed (T* ptr = values)
                {
                    var span = new Span<byte>(ptr, values.Length * sizeof(T));
                    reader.Read(span);
                }
            }
        }

        private static string[]? ReadStringArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
            {
                return null;
            }

            var strings = new string[length];
            for (int i = 0; i < length; ++i)
                strings[i] = reader.ReadString();
            return strings;
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

        private static void WriteArray<T>(T[]? values, BinaryWriter writer)
            where T : unmanaged
        {
            if (values != null)
            {
                writer.Write(values.Length);
                unsafe
                {
                    fixed (T* ptr = values)
                    {
                        var span = new ReadOnlySpan<byte>(ptr, values.Length * sizeof(T));
                        writer.Write(span);
                    }
                }
            }
            else
            {
                writer.Write(0);
            }
        }

        private static void WriteStringArray(string[]? strings, BinaryWriter writer)
        {
            if (strings != null)
            {
                writer.Write(strings.Length);
                for (int i = 0; i < strings.Length; ++i)
                    writer.Write(strings[i]);
            }
            else
            {
                writer.Write(0);
            }
        }
    }
}
