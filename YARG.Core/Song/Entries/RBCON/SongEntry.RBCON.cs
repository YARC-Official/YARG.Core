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
using YARG.Core.Logging;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song
{
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

        private const long NOTE_SNAP_THRESHOLD = 10;

        private RBMetadata _rbMetadata;
        private RBCONDifficulties _rbDifficulties;

        private AbridgedFileInfo_Length? _updateMidi;
        private IRBProUpgrade? _upgrade;

        private AbridgedFileInfo? UpdateMogg;
        private AbridgedFileInfo_Length? UpdateMilo;
        private AbridgedFileInfo_Length? UpdateImage;

        public int RBBandDiff => _rbDifficulties.Band;

        protected abstract DateTime MidiLastUpdate { get; }

        public override DateTime GetAddTime()
        {
            var lastUpdateTime = MidiLastUpdate;
            if (_updateMidi != null)
            {
                if (_updateMidi.Value.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = _updateMidi.Value.LastUpdatedTime;
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
                if (!_updateMidi.Value.IsStillValid(false))
                    return null;

                using var midiStream = new FileStream(_updateMidi.Value.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
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

            return SongChart.FromMidi(_parseSettings, midi);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            var stream = GetMoggStream();
            if (stream == null)
            {
                return null;
            }

            int version = stream.Read<int>(Endianness.Little);
            if (version is not 0x0A and not 0xF0)
            {
                YargLogger.LogError("Original unencrypted mogg replaced by an encrypted mogg!");
                stream.Dispose();
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            bool clampStemVolume = _metadata.Source.Str.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), stream, speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Mogg failed to load!");
                stream.Dispose();
                return null;
            }

            
            if (_rbMetadata.Indices.Drums != null && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (_rbMetadata.Indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        mixer.AddChannel(SongStem.Drums, _rbMetadata.Indices.Drums, _rbMetadata.Panning.Drums!);
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        mixer.AddChannel(SongStem.Drums1, _rbMetadata.Indices.Drums[0..1], _rbMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _rbMetadata.Indices.Drums[1..3], _rbMetadata.Panning.Drums[2..6]);
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        mixer.AddChannel(SongStem.Drums1, _rbMetadata.Indices.Drums[0..1], _rbMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _rbMetadata.Indices.Drums[1..2], _rbMetadata.Panning.Drums[2..4]);
                        mixer.AddChannel(SongStem.Drums3, _rbMetadata.Indices.Drums[2..4], _rbMetadata.Panning.Drums[4..8]);
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        mixer.AddChannel(SongStem.Drums1, _rbMetadata.Indices.Drums[0..1], _rbMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _rbMetadata.Indices.Drums[1..3], _rbMetadata.Panning.Drums[2..6]);
                        mixer.AddChannel(SongStem.Drums3, _rbMetadata.Indices.Drums[3..5], _rbMetadata.Panning.Drums[6..10]);
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        mixer.AddChannel(SongStem.Drums1, _rbMetadata.Indices.Drums[0..2], _rbMetadata.Panning.Drums![0..4]);
                        mixer.AddChannel(SongStem.Drums2, _rbMetadata.Indices.Drums[2..4], _rbMetadata.Panning.Drums[4..8]);
                        mixer.AddChannel(SongStem.Drums3, _rbMetadata.Indices.Drums[4..6], _rbMetadata.Panning.Drums[8..12]);
                        break;
                }
            }

            if (_rbMetadata.Indices.Bass != null && !ignoreStems.Contains(SongStem.Bass))
                mixer.AddChannel(SongStem.Bass, _rbMetadata.Indices.Bass, _rbMetadata.Panning.Bass!);

            if (_rbMetadata.Indices.Guitar != null && !ignoreStems.Contains(SongStem.Guitar))
                mixer.AddChannel(SongStem.Guitar, _rbMetadata.Indices.Guitar, _rbMetadata.Panning.Guitar!);

            if (_rbMetadata.Indices.Keys != null && !ignoreStems.Contains(SongStem.Keys))
                mixer.AddChannel(SongStem.Keys, _rbMetadata.Indices.Keys, _rbMetadata.Panning.Keys!);

            if (_rbMetadata.Indices.Vocals != null && !ignoreStems.Contains(SongStem.Vocals))
                mixer.AddChannel(SongStem.Vocals, _rbMetadata.Indices.Vocals, _rbMetadata.Panning.Vocals!);

            if (_rbMetadata.Indices.Track != null && !ignoreStems.Contains(SongStem.Song))
                mixer.AddChannel(SongStem.Song, _rbMetadata.Indices.Track, _rbMetadata.Panning.Track!);

            if (_rbMetadata.Indices.Crowd != null && !ignoreStems.Contains(SongStem.Crowd))
                mixer.AddChannel(SongStem.Crowd, _rbMetadata.Indices.Crowd, _rbMetadata.Panning.Crowd!);

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

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            var bytes = LoadRawImageData();
            if (bytes == null)
            {
                return null;
            }
            return new YARGImage(bytes);
        }

        public override FixedArray<byte>? LoadMiloData()
        {
            if (UpdateMilo == null || !UpdateMilo.Value.Exists())
            {
                return null;
            }
            return MemoryMappedArray.Load(UpdateMilo.Value);
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

            RBAudio<int>.WriteArray(in _rbMetadata.RealGuitarTuning, writer);
            RBAudio<int>.WriteArray(in _rbMetadata.RealBassTuning, writer);

            _rbMetadata.Indices.Serialize(writer);
            _rbMetadata.Panning.Serialize(writer);

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

        protected abstract bool IsMoggValid(Stream? file);
        protected abstract FixedArray<byte>? LoadMidiFile(Stream? file);
        protected abstract Stream? GetMidiStream();

        protected RBCONEntry() : base()
        {
            _rbMetadata = RBMetadata.Default;
            _rbDifficulties = RBCONDifficulties.Default;
            _parseSettings.DrumsType = DrumsType.FourLane;
            _parseSettings.NoteSnapThreshold = NOTE_SNAP_THRESHOLD;
        }

        protected RBCONEntry(AbridgedFileInfo_Length? updateMidi, IRBProUpgrade? upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            _updateMidi = updateMidi;
            _upgrade = upgrade;

            UpdateMogg =  stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            UpdateMilo =  stream.ReadBoolean() ? new AbridgedFileInfo_Length(stream.ReadString(), false) : null;
            UpdateImage = stream.ReadBoolean() ? new AbridgedFileInfo_Length(stream.ReadString(), false) : null;

            _rbMetadata.AnimTempo = stream.Read<uint>(Endianness.Little);
            _rbMetadata.SongID = stream.ReadString();
            _rbMetadata.VocalPercussionBank = stream.ReadString();
            _rbMetadata.VocalSongScrollSpeed = stream.Read<uint>(Endianness.Little);
            _rbMetadata.VocalGender = stream.ReadBoolean();
            _rbMetadata.VocalTonicNote = stream.Read<uint>(Endianness.Little);
            _rbMetadata.SongTonality = stream.ReadBoolean();
            _rbMetadata.TuningOffsetCents = stream.Read<int>(Endianness.Little);
            _rbMetadata.VenueVersion = stream.Read<uint>(Endianness.Little);
            _rbMetadata.DrumBank = stream.ReadString();

            _rbMetadata.RealGuitarTuning = RBAudio<int>.ReadArray(stream);
            _rbMetadata.RealBassTuning = RBAudio<int>.ReadArray(stream);

            _rbMetadata.Indices = new RBAudio<int>(stream);
            _rbMetadata.Panning = new RBAudio<float>(stream);

            _rbMetadata.Soloes = ReadStringArray(stream);
            _rbMetadata.VideoVenues = ReadStringArray(stream);

            unsafe
            {
                fixed (RBCONDifficulties* ptr = &_rbDifficulties)
                {
                    var span = new Span<byte>(ptr, sizeof(RBCONDifficulties));
                    stream.Read(span);
                }
            }
        }

        protected DTAResult Init(string nodeName, YARGDTAReader reader, Dictionary<string, SortedList<DateTime, SongUpdate>> updates, Dictionary<string, (YARGDTAReader, IRBProUpgrade)> upgrades, string defaultPlaylist)
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


        protected virtual FixedArray<byte>? LoadRawImageData()
        {
            if (UpdateImage != null && UpdateImage.Value.Exists())
            {
                return MemoryMappedArray.Load(UpdateImage.Value);
            }
            return null;
        }

        protected virtual Stream? GetMoggStream()
        {
            if (UpdateMogg == null)
            {
                return null;
            }

            var mogg = UpdateMogg.Value;
            if (!File.Exists(mogg.FullName))
            {
                return null;
            }

            if (mogg.FullName.EndsWith(".yarg_mogg"))
            {
                return new YargMoggReadStream(mogg.FullName);
            }
            return new FileStream(mogg.FullName, FileMode.Open, FileAccess.Read);
        }

        protected FixedArray<byte>? LoadUpdateMidiFile()
        {
            if (_updateMidi == null || !_updateMidi.Value.IsStillValid(false))
            {
                return null;
            }
            return MemoryMappedArray.Load(_updateMidi.Value);
        }

        protected ScanResult ParseRBCONMidi(Stream? file)
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
                using var chartFile = LoadMidiFile(file);
                using var updateFile = LoadUpdateMidiFile();
                using var upgradeFile = _upgrade?.LoadUpgradeMidi();

                DrumPreparseHandler drumTracker = new()
                {
                    Type = DrumsType.ProDrums
                };

                long bufLength = 0;
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

                using var buffer = AllocatedArray<byte>.Alloc(bufLength);
                unsafe
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr, chartFile.Ptr, (uint) chartFile.Length);

                    long offset = chartFile.Length;
                    if (updateFile != null)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, updateFile.Ptr, (uint) updateFile.Length);
                        offset += updateFile.Length;
                    }

                    if (upgradeFile != null)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, upgradeFile.Ptr, (uint) upgradeFile.Length);
                    }
                }
                _hash = HashWrapper.Hash(buffer.ReadOnlySpan);
                return ScanResult.Success;
            }
            catch
            {
                return ScanResult.PossibleCorruption;
            }
        }

        private void ApplyRBCONUpdates(ref DTAResult mainResult, string nodeName, Dictionary<string, SortedList<DateTime, SongUpdate>> updates)
        {
            if (updates.TryGetValue(nodeName, out var updateList))
            {
                foreach (var update in updateList.Values)
                {
                    try
                    {
                        var updateResults = ParseDTA(nodeName, update.Readers);
                        Update(in update, nodeName, updateResults);

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
                        YargLogger.LogException(ex, $"Error processing CON Update {update.BaseDirectory} - {nodeName}!");
                    }
                }
            }
        }

        private void ApplyRBProUpgrade(string nodeName, Dictionary<string, (YARGDTAReader, IRBProUpgrade)> upgrades)
        {
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                try
                {
                    ParseDTA(nodeName, upgrade.Item1);
                    _upgrade = upgrade.Item2;
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error processing CON Upgrade {nodeName}!");
                }
            }
        }

        private DTAResult ParseDTA(string nodeName, params YARGDTAReader[] readers)
        {
            DTAResult result = default;
            for (int i = 0; i < readers.Length; ++i)
            {
                var reader = readers[i];
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode(false);
                    switch (name)
                    {
                        case "name": _metadata.Name = reader.ExtractText(); break;
                        case "artist": _metadata.Artist = reader.ExtractText(); break;
                        case "master": _metadata.IsMaster = reader.ExtractBoolean_FlippedDefault(); break;
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
                            _metadata.PreviewStart = reader.ExtractInt64();
                            _metadata.PreviewEnd = reader.ExtractInt64();
                            break;
                        case "rank": DifficultyLoop(reader); break;
                        case "solo": _rbMetadata.Soloes = reader.ExtractArray_String(); break;
                        case "genre": _metadata.Genre = reader.ExtractText(); break;
                        case "decade": /*Decade = reader.ExtractText();*/ break;
                        case "vocal_gender": _rbMetadata.VocalGender = reader.ExtractText() == "male"; break;
                        case "format": /*Format = reader.Read<uint>();*/ break;
                        case "version": _rbMetadata.VenueVersion = reader.ExtractUInt32(); break;
                        case "fake": /*IsFake = reader.ExtractText();*/ break;
                        case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                        case "game_origin":
                            {
                                string str = reader.ExtractText();
                                if ((str == "ugc" || str == "ugc_plus"))
                                {
                                    if (!nodeName.StartsWith("UGC_"))
                                        _metadata.Source = "customs";
                                }
                                else if (str == "#ifdef")
                                {
                                    string conditional = reader.ExtractText();
                                    if (conditional == "CUSTOMSOURCE")
                                    {
                                        _metadata.Source = reader.ExtractText();
                                    }
                                    else
                                    {
                                        _metadata.Source = "customs";
                                    }
                                }
                                else
                                {
                                    _metadata.Source = str;
                                }

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
                        case "album_track_number": _metadata.AlbumTrack = reader.ExtractInt32(); break;
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
                                _ => reader.Encoding
                            };

                            if (reader.Encoding != encoding)
                            {
                                string Convert(string str)
                                {
                                    byte[] bytes = reader.Encoding.GetBytes(str);
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
                                reader.Encoding = encoding;
                            }

                            break;
                        case "vocal_tonic_note": _rbMetadata.VocalTonicNote = reader.ExtractUInt32(); break;
                        case "song_tonality": _rbMetadata.SongTonality = reader.ExtractBoolean(); break;
                        case "alternate_path": result.alternatePath = reader.ExtractBoolean(); break;
                        case "real_guitar_tuning": _rbMetadata.RealGuitarTuning = reader.ExtractArray_Int(); break;
                        case "real_bass_tuning": _rbMetadata.RealBassTuning = reader.ExtractArray_Int(); break;
                        case "video_venues": _rbMetadata.VideoVenues = reader.ExtractArray_String(); break;
                        case "extra_authoring":
                            {
                                StringBuilder authors = new();
                                foreach (string str in reader.ExtractArray_String())
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
                string descriptor = reader.GetNameOfNode(false);
                switch (descriptor)
                {
                    case "name": result.location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(reader); break;
                    case "crowd_channels": _rbMetadata.Indices.Crowd = reader.ExtractArray_Int();  break;
                    //case "vocal_parts": VocalParts = reader.Read<ushort>(); break;
                    case "pans":  result.pans =    reader.ExtractArray_Float(); break;
                    case "vols":  result.volumes = reader.ExtractArray_Float(); break;
                    case "cores": result.cores =   reader.ExtractArray_Float(); break;
                    case "hopo_threshold": _parseSettings.HopoThreshold = reader.ExtractInt64(); break;
                }
                reader.EndNode();
            }
        }

        private void TracksLoop(YARGDTAReader reader)
        {
            _rbMetadata.Indices = new()
            {
                Crowd = _rbMetadata.Indices.Crowd
            };
            while (reader.StartNode())
            {
                while (reader.StartNode())
                {
                    switch (reader.GetNameOfNode(false))
                    {
                        case "drum"  : _rbMetadata.Indices.Drums  = reader.ExtractArray_Int(); break;
                        case "bass"  : _rbMetadata.Indices.Bass   = reader.ExtractArray_Int(); break;
                        case "guitar": _rbMetadata.Indices.Guitar = reader.ExtractArray_Int(); break;
                        case "keys"  : _rbMetadata.Indices.Keys   = reader.ExtractArray_Int(); break;
                        case "vocals": _rbMetadata.Indices.Vocals = reader.ExtractArray_Int(); break;
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
                string name = reader.GetNameOfNode(false);
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

        private void Update(in SongUpdate update, string nodename, in DTAResult results)
        {
            if (results.discUpdate)
            {
                if (update.Midi != null)
                {
                    if (_updateMidi == null || update.Midi.Value.LastUpdatedTime > _updateMidi.Value.LastUpdatedTime)
                    {
                        _updateMidi = update.Midi;
                    }
                }
                else
                {
                    YargLogger.LogFormatWarning("Update midi expected in directory {0}", Path.Combine(update.BaseDirectory, nodename));
                }
            }

            if (update.Mogg != null)
            {
                if (UpdateMogg == null || update.Mogg.Value.LastUpdatedTime > UpdateMogg.Value.LastUpdatedTime)
                {
                    UpdateMogg = update.Mogg;
                }
            }

            if (update.Milo != null)
            {
                if (UpdateMilo == null || update.Milo.Value.LastUpdatedTime > UpdateMilo.Value.LastUpdatedTime)
                {
                    UpdateMilo = update.Milo;
                }
            }

            if (results.alternatePath)
            {
                if (update.Image != null)
                {
                    if (UpdateImage == null || update.Image.Value.LastUpdatedTime > UpdateImage.Value.LastUpdatedTime)
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

            if (_rbMetadata.Indices.Drums != null)
                _rbMetadata.Panning.Drums = CalculateStemValues(_rbMetadata.Indices.Drums);

            if (_rbMetadata.Indices.Bass != null)
                _rbMetadata.Panning.Bass = CalculateStemValues(_rbMetadata.Indices.Bass);

            if (_rbMetadata.Indices.Guitar != null)
                _rbMetadata.Panning.Guitar = CalculateStemValues(_rbMetadata.Indices.Guitar);

            if (_rbMetadata.Indices.Keys != null)
                _rbMetadata.Panning.Keys = CalculateStemValues(_rbMetadata.Indices.Keys);

            if (_rbMetadata.Indices.Vocals != null)
                _rbMetadata.Panning.Vocals = CalculateStemValues(_rbMetadata.Indices.Vocals);

            if (_rbMetadata.Indices.Crowd != null)
                _rbMetadata.Panning.Crowd = CalculateStemValues(_rbMetadata.Indices.Crowd);

            if (pending.Count > 0)
            {
                _rbMetadata.Indices.Track = pending.ToArray();
                _rbMetadata.Panning.Track = CalculateStemValues(_rbMetadata.Indices.Track);
            }

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

        private static AbridgedFileInfo? ReadUpdateInfo(UnmanagedMemoryStream stream)
        {
            if (!stream.ReadBoolean())
            {
                return null;
            }
            return new AbridgedFileInfo(stream.ReadString(), false);
        }

        private static string[]? ReadStringArray(UnmanagedMemoryStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return null;
            }

            var strings = new string[length];
            for (int i = 0; i < length; ++i)
                strings[i] = stream.ReadString();
            return strings;
        }

        private static void WriteUpdateInfo<TInfo>(TInfo? info, BinaryWriter writer)
            where TInfo : struct, IAbridgedInfo
        {
            if (info != null)
            {
                writer.Write(true);
                writer.Write(info.Value.FullName);
            }
            else
                writer.Write(false);
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
