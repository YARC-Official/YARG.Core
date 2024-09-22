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

namespace YARG.Core.Song
{
    public abstract class RBCONEntry : SongEntry
    {
        protected struct DTAResult
        {
            public static readonly DTAResult Empty = new()
            {
                pans = Array.Empty<float>(),
                volumes = Array.Empty<float>(),
                cores = Array.Empty<float>(),
            };

            public bool alternatePath;
            public bool discUpdate;
            public string location;
            public float[] pans;
            public float[] volumes;
            public float[] cores;
        }

        private const long NOTE_SNAP_THRESHOLD = 10;

        private RBMetadata _rbMetadata;
        private RBCONDifficulties _rbDifficulties;

        private AbridgedFileInfo? _updateMidi;
        private RBProUpgrade? _upgrade;

        private AbridgedFileInfo? UpdateMogg;
        private AbridgedFileInfo? UpdateMilo;
        private AbridgedFileInfo? UpdateImage;

        public string RBSongId => _rbMetadata.SongID;
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


            if (_rbMetadata.Indices.Drums.Length > 0 && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (_rbMetadata.Indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 1:
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

            if (_rbMetadata.Indices.Bass.Length > 0 && !ignoreStems.Contains(SongStem.Bass))
                mixer.AddChannel(SongStem.Bass, _rbMetadata.Indices.Bass, _rbMetadata.Panning.Bass!);

            if (_rbMetadata.Indices.Guitar.Length > 0 && !ignoreStems.Contains(SongStem.Guitar))
                mixer.AddChannel(SongStem.Guitar, _rbMetadata.Indices.Guitar, _rbMetadata.Panning.Guitar!);

            if (_rbMetadata.Indices.Keys.Length > 0 && !ignoreStems.Contains(SongStem.Keys))
                mixer.AddChannel(SongStem.Keys, _rbMetadata.Indices.Keys, _rbMetadata.Panning.Keys!);

            if (_rbMetadata.Indices.Vocals.Length > 0 && !ignoreStems.Contains(SongStem.Vocals))
                mixer.AddChannel(SongStem.Vocals, _rbMetadata.Indices.Vocals, _rbMetadata.Panning.Vocals!);

            if (_rbMetadata.Indices.Track.Length > 0 && !ignoreStems.Contains(SongStem.Song))
                mixer.AddChannel(SongStem.Song, _rbMetadata.Indices.Track, _rbMetadata.Panning.Track!);

            if (_rbMetadata.Indices.Crowd.Length > 0 && !ignoreStems.Contains(SongStem.Crowd))
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
            return bytes.IsAllocated ? new YARGImage(bytes) : null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
           return UpdateMilo != null && UpdateMilo.Value.Exists()
                ? FixedArray<byte>.Load(UpdateMilo.Value.FullName)
                : FixedArray<byte>.Default;
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
        protected abstract FixedArray<byte> LoadMidiFile(Stream? file);
        protected abstract Stream? GetMidiStream();

        protected RBCONEntry() : base()
        {
            _rbMetadata = RBMetadata.Default;
            _rbDifficulties = RBCONDifficulties.Default;
            _parseSettings.DrumsType = DrumsType.FourLane;
            _parseSettings.NoteSnapThreshold = NOTE_SNAP_THRESHOLD;
        }

        protected RBCONEntry(AbridgedFileInfo? updateMidi, RBProUpgrade? upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            _updateMidi = updateMidi;
            _upgrade = upgrade;

            UpdateMogg =  stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            UpdateMilo =  stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            UpdateImage = stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;

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

        protected DTAResult Init(string nodeName, in YARGTextContainer<byte> container, Dictionary<string, SortedList<DateTime, SongUpdate>> updates, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades, string defaultPlaylist)
        {
            var dtaResults = ParseDTA(nodeName, container);
            ApplyRBCONUpdates(ref dtaResults, nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);

            if (dtaResults.pans.Length == 0 || dtaResults.volumes.Length == 0 || dtaResults.cores.Length == 0)
            {
                throw new Exception("Panning & Volume mappings not set from DTA");
            }
            FinalizeRBCONAudioValues(in dtaResults);

            if (_metadata.Playlist.Length == 0)
                _metadata.Playlist = defaultPlaylist;
            return dtaResults;
        }


        protected virtual FixedArray<byte> LoadRawImageData()
        {
            return UpdateImage != null && UpdateImage.Value.Exists()
                ? FixedArray<byte>.Load(UpdateImage.Value.FullName)
                : FixedArray<byte>.Default;
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

        protected FixedArray<byte> LoadUpdateMidiFile()
        {
            return _updateMidi != null && _updateMidi.Value.IsStillValid(false)
               ? FixedArray<byte>.Load(_updateMidi.Value.FullName)
               : FixedArray<byte>.Default;
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
                using var upgradeFile = _upgrade != null ? _upgrade.LoadUpgradeMidi() : FixedArray<byte>.Default;

                DrumPreparseHandler drumTracker = new()
                {
                    Type = DrumsType.ProDrums
                };

                long bufLength = 0;
                if (_updateMidi != null)
                {
                    if (!updateFile.IsAllocated)
                        return ScanResult.MissingUpdateMidi;

                    if (!ParseMidi(in updateFile, drumTracker, ref _parts))
                        return ScanResult.MultipleMidiTrackNames_Update;

                    bufLength += updateFile.Length;
                }

                if (_upgrade != null)
                {
                    if (!upgradeFile.IsAllocated)
                        return ScanResult.MissingUpgradeMidi;

                    if (!ParseMidi(in upgradeFile, drumTracker, ref _parts))
                        return ScanResult.MultipleMidiTrackNames_Upgrade;

                    bufLength += upgradeFile.Length;
                }

                if (!chartFile.IsAllocated)
                    return ScanResult.MissingMidi;

                if (!ParseMidi(in chartFile, drumTracker, ref _parts))
                    return ScanResult.MultipleMidiTrackNames;

                bufLength += chartFile.Length;

                SetDrums(ref _parts, drumTracker);
                if (!CheckScanValidity(in _parts))
                {
                    return ScanResult.NoNotes;
                }

                using var buffer = FixedArray<byte>.Alloc(bufLength);
                unsafe
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr, chartFile.Ptr, (uint) chartFile.Length);

                    long offset = chartFile.Length;
                    if (updateFile.IsAllocated)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, updateFile.Ptr, (uint) updateFile.Length);
                        offset += updateFile.Length;
                    }

                    if (upgradeFile.IsAllocated)
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
                        var updateResults = ParseDTA(nodeName, update.Containers);
                        Update(update, nodeName, updateResults);

                        if (updateResults.cores.Length > 0)
                        {
                            mainResult.cores = updateResults.cores;
                        }

                        if (updateResults.volumes.Length > 0)
                        {
                            mainResult.volumes = updateResults.volumes;
                        }

                        if (updateResults.pans.Length > 0)
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

        private void ApplyRBProUpgrade(string nodeName, Dictionary<string, (YARGTextContainer<byte> Container, RBProUpgrade Upgrade)> upgrades)
        {
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                try
                {
                    ParseDTA(nodeName, upgrade.Container);
                    _upgrade = upgrade.Upgrade;
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, $"Error processing CON Upgrade {nodeName}!");
                }
            }
        }

        private DTAResult ParseDTA(string nodeName, params YARGTextContainer<byte>[] containers)
        {
            var result = DTAResult.Empty;
            for (int i = 0; i < containers.Length; ++i)
            {
                var container = containers[i];
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, false);
                    switch (name)
                    {
                        case "name": _metadata.Name = YARGDTAReader.ExtractText(ref container); break;
                        case "artist": _metadata.Artist = YARGDTAReader.ExtractText(ref container); break;
                        case "master": _metadata.IsMaster = YARGDTAReader.ExtractBoolean_FlippedDefault(ref container); break;
                        case "context": /*Context = container.Read<uint>();*/ break;
                        case "song": SongLoop(ref result, ref container); break;
                        case "song_vocals": while (YARGDTAReader.StartNode(ref container)) YARGDTAReader.EndNode(ref container); break;
                        case "song_scroll_speed": _rbMetadata.VocalSongScrollSpeed = YARGDTAReader.ExtractUInt32(ref container); break;
                        case "tuning_offset_cents": _rbMetadata.TuningOffsetCents = YARGDTAReader.ExtractInt32(ref container); break;
                        case "bank": _rbMetadata.VocalPercussionBank = YARGDTAReader.ExtractText(ref container); break;
                        case "anim_tempo":
                            {
                                string val = YARGDTAReader.ExtractText(ref container);
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
                            _metadata.PreviewStart = YARGDTAReader.ExtractInt64(ref container);
                            _metadata.PreviewEnd = YARGDTAReader.ExtractInt64(ref container);
                            break;
                        case "rank": DifficultyLoop(ref container); break;
                        case "solo": _rbMetadata.Soloes = YARGDTAReader.ExtractArray_String(ref container); break;
                        case "genre": _metadata.Genre = YARGDTAReader.ExtractText(ref container); break;
                        case "decade": /*Decade = container.ExtractText();*/ break;
                        case "vocal_gender": _rbMetadata.VocalGender = YARGDTAReader.ExtractText(ref container) == "male"; break;
                        case "format": /*Format = container.Read<uint>();*/ break;
                        case "version": _rbMetadata.VenueVersion = YARGDTAReader.ExtractUInt32(ref container); break;
                        case "fake": /*IsFake = container.ExtractText();*/ break;
                        case "downloaded": /*Downloaded = container.ExtractText();*/ break;
                        case "game_origin":
                            {
                                string str = YARGDTAReader.ExtractText(ref container);
                                if ((str == "ugc" || str == "ugc_plus"))
                                {
                                    if (!nodeName.StartsWith("UGC_"))
                                        _metadata.Source = "customs";
                                }
                                else if (str == "#ifdef")
                                {
                                    string conditional = YARGDTAReader.ExtractText(ref container);
                                    if (conditional == "CUSTOMSOURCE")
                                    {
                                        _metadata.Source = YARGDTAReader.ExtractText(ref container);
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
                        case "song_id": _rbMetadata.SongID = YARGDTAReader.ExtractText(ref container); break;
                        case "rating": _metadata.SongRating = YARGDTAReader.ExtractUInt32(ref container); break;
                        case "short_version": /*ShortVersion = container.Read<uint>();*/ break;
                        case "album_art": /*HasAlbumArt = container.ExtractBoolean();*/ break;
                        case "year_released":
                        case "year_recorded": YearAsNumber = YARGDTAReader.ExtractInt32(ref container); break;
                        case "album_name": _metadata.Album = YARGDTAReader.ExtractText(ref container); break;
                        case "album_track_number": _metadata.AlbumTrack = YARGDTAReader.ExtractInt32(ref container); break;
                        case "pack_name": _metadata.Playlist = YARGDTAReader.ExtractText(ref container); break;
                        case "base_points": /*BasePoints = container.Read<uint>();*/ break;
                        case "band_fail_cue": /*BandFailCue = container.ExtractText();*/ break;
                        case "drum_bank": _rbMetadata.DrumBank = YARGDTAReader.ExtractText(ref container); break;
                        case "song_length": _metadata.SongLength = YARGDTAReader.ExtractUInt64(ref container); break;
                        case "sub_genre": /*Subgenre = container.ExtractText();*/ break;
                        case "author": _metadata.Charter = YARGDTAReader.ExtractText(ref container); break;
                        case "guide_pitch_volume": /*GuidePitchVolume = container.ReadFloat();*/ break;
                        case "encoding":
                            var encoding = YARGDTAReader.ExtractText(ref container).ToLower() switch
                            {
                                "latin1" => YARGTextReader.Latin1,
                                "utf-8" or
                                "utf8" => Encoding.UTF8,
                                _ => container.Encoding
                            };

                            if (container.Encoding != encoding)
                            {
                                string Convert(string str)
                                {
                                    byte[] bytes = container.Encoding.GetBytes(str);
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
                                container.Encoding = encoding;
                            }

                            break;
                        case "vocal_tonic_note": _rbMetadata.VocalTonicNote = YARGDTAReader.ExtractUInt32(ref container); break;
                        case "song_tonality": _rbMetadata.SongTonality = YARGDTAReader.ExtractBoolean(ref container); break;
                        case "alternate_path": result.alternatePath = YARGDTAReader.ExtractBoolean(ref container); break;
                        case "real_guitar_tuning": _rbMetadata.RealGuitarTuning = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "real_bass_tuning": _rbMetadata.RealBassTuning = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "video_venues": _rbMetadata.VideoVenues = YARGDTAReader.ExtractArray_String(ref container); break;
                        case "extra_authoring":
                            {
                                StringBuilder authors = new();
                                foreach (string str in YARGDTAReader.ExtractArray_String(ref container))
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
                    YARGDTAReader.EndNode(ref container);
                }
            }
            return result;
        }

        private void SongLoop(ref DTAResult result, ref YARGTextContainer<byte> container)
        {
            while (YARGDTAReader.StartNode(ref container))
            {
                string descriptor = YARGDTAReader.GetNameOfNode(ref container, false);
                switch (descriptor)
                {
                    case "name": result.location = YARGDTAReader.ExtractText(ref container); break;
                    case "tracks": TracksLoop(ref container); break;
                    case "crowd_channels": _rbMetadata.Indices.Crowd = YARGDTAReader.ExtractArray_Int(ref container);  break;
                    //case "vocal_parts": VocalParts = container.Read<ushort>(); break;
                    case "pans":  result.pans =    YARGDTAReader.ExtractArray_Float(ref container); break;
                    case "vols":  result.volumes = YARGDTAReader.ExtractArray_Float(ref container); break;
                    case "cores": result.cores =   YARGDTAReader.ExtractArray_Float(ref container); break;
                    case "hopo_threshold": _parseSettings.HopoThreshold = YARGDTAReader.ExtractInt64(ref container); break;
                }
                YARGDTAReader.EndNode(ref container);
            }
        }

        private void TracksLoop(ref YARGTextContainer<byte> container)
        {
            var crowd = _rbMetadata.Indices.Crowd;
            _rbMetadata.Indices = RBAudio<int>.Empty;
            _rbMetadata.Indices.Crowd = crowd;
            while (YARGDTAReader.StartNode(ref container))
            {
                while (YARGDTAReader.StartNode(ref container))
                {
                    switch (YARGDTAReader.GetNameOfNode(ref container, false))
                    {
                        case "drum"  : _rbMetadata.Indices.Drums  = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "bass"  : _rbMetadata.Indices.Bass   = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "guitar": _rbMetadata.Indices.Guitar = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "keys"  : _rbMetadata.Indices.Keys   = YARGDTAReader.ExtractArray_Int(ref container); break;
                        case "vocals": _rbMetadata.Indices.Vocals = YARGDTAReader.ExtractArray_Int(ref container); break;
                    }
                    YARGDTAReader.EndNode(ref container);
                }
                YARGDTAReader.EndNode(ref container);
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

        private void DifficultyLoop(ref YARGTextContainer<byte> container)
        {
            int diff;
            while (YARGDTAReader.StartNode(ref container))
            {
                string name = YARGDTAReader.GetNameOfNode(ref container, false);
                diff = YARGDTAReader.ExtractInt32(ref container);
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
                YARGDTAReader.EndNode(ref container);
            }
        }

        private static void SetRank(ref sbyte intensity, int rank, int[] values)
        {
            sbyte i = 0;
            while (i < 6 && values[i] <= rank)
                ++i;
            intensity = i;
        }

        private void Update(SongUpdate update, string nodename, in DTAResult results)
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

        private void FinalizeRBCONAudioValues(in DTAResult result)
        {
            HashSet<int> pending = new();
            for (int i = 0; i < result.pans.Length; i++)
                pending.Add(i);

            if (_rbMetadata.Indices.Drums.Length > 0)
                _rbMetadata.Panning.Drums = CalculateStemValues(_rbMetadata.Indices.Drums, in result, pending);

            if (_rbMetadata.Indices.Bass.Length > 0)
                _rbMetadata.Panning.Bass = CalculateStemValues(_rbMetadata.Indices.Bass, in result, pending);

            if (_rbMetadata.Indices.Guitar.Length > 0)
                _rbMetadata.Panning.Guitar = CalculateStemValues(_rbMetadata.Indices.Guitar, in result, pending);

            if (_rbMetadata.Indices.Keys.Length > 0)
                _rbMetadata.Panning.Keys = CalculateStemValues(_rbMetadata.Indices.Keys, in result, pending);

            if (_rbMetadata.Indices.Vocals.Length > 0)
                _rbMetadata.Panning.Vocals = CalculateStemValues(_rbMetadata.Indices.Vocals, in result, pending);

            if (_rbMetadata.Indices.Crowd.Length > 0)
                _rbMetadata.Panning.Crowd = CalculateStemValues(_rbMetadata.Indices.Crowd, in result, pending);

            if (pending.Count > 0)
            {
                _rbMetadata.Indices.Track = pending.ToArray();
                _rbMetadata.Panning.Track = CalculateStemValues(_rbMetadata.Indices.Track, in result, pending);
            }
        }

        private float[] CalculateStemValues(int[] indices, in DTAResult result, HashSet<int> pending)
        {
            float[] values = new float[2 * indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                float theta = (result.pans[index] + 1) * ((float) Math.PI / 4);
                float volRatio = (float) Math.Pow(10, result.volumes[index] / 20);
                values[2 * i] = volRatio * (float) Math.Cos(theta);
                values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                pending.Remove(index);
            }
            return values;
        }

        private static AbridgedFileInfo? ReadUpdateInfo(UnmanagedMemoryStream stream)
        {
            if (!stream.ReadBoolean())
            {
                return null;
            }
            return new AbridgedFileInfo(stream.ReadString(), false);
        }

        private static string[] ReadStringArray(UnmanagedMemoryStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<string>();
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

        private static void WriteStringArray(string[] strings, BinaryWriter writer)
        {
            writer.Write(strings.Length);
            for (int i = 0; i < strings.Length; ++i)
            {
                writer.Write(strings[i]);
            }
        }
    }
}
