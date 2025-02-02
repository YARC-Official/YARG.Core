using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Extensions;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    public struct RBScanParameters
    {
        public DTAEntry UpdateDta;
        public DTAEntry UpgradeDta;
        public AbridgedFileInfo Root;
        public string NodeName;
        public AbridgedFileInfo? UpdateDirectory;
        public DateTime? UpdateMidi;
        public RBProUpgrade? Upgrade;
        public string DefaultPlaylist;
        public DTAEntry BaseDta;
    }

    public abstract class RBCONEntry : SongEntry
    {
        private const long NOTE_SNAP_THRESHOLD = 10;
        public const int UNENCRYPTED_MOGG = 0xA;

        protected readonly AbridgedFileInfo _root;
        protected readonly string _nodeName;
        protected string _subName = string.Empty;
        protected AbridgedFileInfo? _updateDirectoryAndDtaLastWrite;
        protected DateTime? _updateMidiLastWrite;
        protected RBProUpgrade? _upgrade;

        protected RBMetadata _rbMetadata = RBMetadata.Default;
        protected RBIntensities _rbIntensities = RBIntensities.Default;
        protected RBAudio<int> _indices = RBAudio<int>.Empty;
        protected RBAudio<float> _panning = RBAudio<float>.Empty;

        public string RBSongId => _rbMetadata.SongID;
        public int RBBandDiff => _rbIntensities.Band;

        protected abstract DateTime MidiLastWriteTime { get; }

        protected abstract FixedArray<byte> GetMainMidiData();
        protected abstract Stream? GetMoggStream();

        public override DateTime GetLastWriteTime()
        {
            var last_write = MidiLastWriteTime;
            if (_updateMidiLastWrite.HasValue && _updateMidiLastWrite > last_write)
            {
                last_write = _updateMidiLastWrite.Value;
            }

            if (_upgrade != null && _upgrade.LastWriteTime > last_write)
            {
                last_write = _upgrade.LastWriteTime;
            }
            return last_write;
        }

        public override void Serialize(MemoryStream stream, CacheWriteIndices indices)
        {
            base.Serialize(stream, indices);
            stream.Write(_yearAsNumber, Endianness.Little);

            unsafe
            {
                var intensities = _rbIntensities;
                stream.Write(new ReadOnlySpan<byte>(&intensities, sizeof(RBIntensities)));
            }

            stream.WriteByte((byte)_rbMetadata.VocalGender);
            stream.WriteByte((byte)_rbMetadata.SongTonality);
            stream.WriteByte((byte)_rbMetadata.MidiEncoding);

            stream.Write(_rbMetadata.AnimTempo,            Endianness.Little);
            stream.Write(_rbMetadata.VocalSongScrollSpeed, Endianness.Little);
            stream.Write(_rbMetadata.VocalTonicNote,       Endianness.Little);
            stream.Write(_rbMetadata.TuningOffsetCents,    Endianness.Little);
            stream.Write(_rbMetadata.VenueVersion,         Endianness.Little);

            stream.Write(_rbMetadata.SongID);
            stream.Write(_rbMetadata.VocalPercussionBank);
            stream.Write(_rbMetadata.DrumBank);

            WriteArray(in _rbMetadata.RealGuitarTuning, stream);
            WriteArray(in _rbMetadata.RealBassTuning,   stream);

            WriteArray(in _rbMetadata.Soloes,      stream);
            WriteArray(in _rbMetadata.VideoVenues, stream);

            WriteAudio(in _indices, stream);
            WriteAudio(in _panning, stream);
        }

        public override SongChart? LoadChart()
        {
            MidiFile midi;
            var readingSettings = MidiSettingsLatin1.Instance; // RBCONs are always Latin-1
            // Read base MIDI
            using (var mainMidi = GetMainMidiData())
            {
                if (!mainMidi.IsAllocated)
                {
                    return null;
                }
                midi = MidiFile.Read(mainMidi.ToReferenceStream(), readingSettings);
            }

            // Merge update MIDI
            if (_updateMidiLastWrite.HasValue)
            {
                if (!AbridgedFileInfo.Validate(Path.Combine(_updateDirectoryAndDtaLastWrite!.Value.FullName, "songs_updates.dta"), _updateMidiLastWrite.Value))
                {
                    return null;
                }

                string updateFilename = Path.Combine(_updateDirectoryAndDtaLastWrite!.Value.FullName, _nodeName, _nodeName + "_update.mid");
                if (!AbridgedFileInfo.Validate(updateFilename, _updateMidiLastWrite.Value))
                {
                    return null;
                }

                using var updateMidi = FixedArray.LoadFile(updateFilename);
                var update = MidiFile.Read(updateMidi.ToReferenceStream(), readingSettings);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (_upgrade != null)
            {
                using var upgradeMidi = _upgrade.LoadUpgradeMidi();
                if (!upgradeMidi.IsAllocated)
                {
                    return null;
                }

                var upgrade = MidiFile.Read(upgradeMidi.ToReferenceStream(), readingSettings);
                midi.Merge(upgrade);
            }

            var parseSettings = new ParseSettings()
            {
                HopoThreshold = _settings.HopoThreshold,
                SustainCutoffThreshold = _settings.SustainCutoffThreshold,
                StarPowerNote = _settings.OverdiveMidiNote,
                DrumsType = DrumsType.FourLane,
                ChordHopoCancellation = true
            };
            return SongChart.FromMidi(in parseSettings, midi);
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

            bool clampStemVolume = _metadata.Source.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), stream, speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Mogg failed to load!");
                stream.Dispose();
                return null;
            }


            if (_indices.Drums.Length > 0 && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (_indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 1:
                    case 2:
                        mixer.AddChannel(SongStem.Drums, _indices.Drums, _panning.Drums!);
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        mixer.AddChannel(SongStem.Drums1, _indices.Drums[0..1], _panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _indices.Drums[1..3], _panning.Drums[2..6]);
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        mixer.AddChannel(SongStem.Drums1, _indices.Drums[0..1], _panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _indices.Drums[1..2], _panning.Drums[2..4]);
                        mixer.AddChannel(SongStem.Drums3, _indices.Drums[2..4], _panning.Drums[4..8]);
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        mixer.AddChannel(SongStem.Drums1, _indices.Drums[0..1], _panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, _indices.Drums[1..3], _panning.Drums[2..6]);
                        mixer.AddChannel(SongStem.Drums3, _indices.Drums[3..5], _panning.Drums[6..10]);
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        mixer.AddChannel(SongStem.Drums1, _indices.Drums[0..2], _panning.Drums![0..4]);
                        mixer.AddChannel(SongStem.Drums2, _indices.Drums[2..4], _panning.Drums[4..8]);
                        mixer.AddChannel(SongStem.Drums3, _indices.Drums[4..6], _panning.Drums[8..12]);
                        break;
                }
            }

            if (_indices.Bass.Length > 0 && !ignoreStems.Contains(SongStem.Bass))
                mixer.AddChannel(SongStem.Bass, _indices.Bass, _panning.Bass!);

            if (_indices.Guitar.Length > 0 && !ignoreStems.Contains(SongStem.Guitar))
                mixer.AddChannel(SongStem.Guitar, _indices.Guitar, _panning.Guitar!);

            if (_indices.Keys.Length > 0 && !ignoreStems.Contains(SongStem.Keys))
                mixer.AddChannel(SongStem.Keys, _indices.Keys, _panning.Keys!);

            if (_indices.Vocals.Length > 0 && !ignoreStems.Contains(SongStem.Vocals))
                mixer.AddChannel(SongStem.Vocals, _indices.Vocals, _panning.Vocals!);

            if (_indices.Track.Length > 0 && !ignoreStems.Contains(SongStem.Song))
                mixer.AddChannel(SongStem.Song, _indices.Track, _panning.Track!);

            if (_indices.Crowd.Length > 0 && !ignoreStems.Contains(SongStem.Crowd))
                mixer.AddChannel(SongStem.Crowd, _indices.Crowd, _panning.Crowd!);

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

        public void UpdateInfo(in AbridgedFileInfo? updateDirectory, in DateTime? updateMidi, RBProUpgrade? upgrade)
        {
            _updateDirectoryAndDtaLastWrite = updateDirectory;
            _updateMidiLastWrite = updateMidi;
            _upgrade = upgrade;
        }

        protected new void Deserialize(ref FixedArrayStream stream, CacheReadStrings strings)
        {
            base.Deserialize(ref stream, strings);
            _yearAsNumber = stream.Read<int>(Endianness.Little);
            _parsedYear = _metadata.Year;

            unsafe
            {
                RBIntensities intensities;
                stream.Read(&intensities, sizeof(RBIntensities));
                _rbIntensities = intensities;
            }

            _rbMetadata.VocalGender  = (VocalGender) stream.ReadByte();
            _rbMetadata.SongTonality = (SongTonality)stream.ReadByte();
            _rbMetadata.MidiEncoding = (EncodingType)stream.ReadByte();

            _rbMetadata.AnimTempo            = stream.Read<uint>(Endianness.Little);
            _rbMetadata.VocalSongScrollSpeed = stream.Read<uint>(Endianness.Little);
            _rbMetadata.VocalTonicNote       = stream.Read<uint>(Endianness.Little);
            _rbMetadata.TuningOffsetCents    = stream.Read<int> (Endianness.Little);
            _rbMetadata.VenueVersion         = stream.Read<uint>(Endianness.Little);

            _rbMetadata.SongID              = stream.ReadString();
            _rbMetadata.VocalPercussionBank = stream.ReadString();
            _rbMetadata.DrumBank            = stream.ReadString();

            _rbMetadata.RealGuitarTuning = ReadArray<int>(ref stream);
            _rbMetadata.RealBassTuning   = ReadArray<int>(ref stream);

            _rbMetadata.Soloes      = ReadStringArray(ref stream);
            _rbMetadata.VideoVenues = ReadStringArray(ref stream);

            ReadAudio(ref _indices, ref stream);
            ReadAudio(ref _panning, ref stream);
        }

        protected RBCONEntry(in AbridgedFileInfo root, string nodeName)
        {
            _root = root;
            _nodeName = nodeName;
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
        protected static ScanExpected<string> ProcessDTAs(RBCONEntry entry, in DTAEntry baseDTA, in DTAEntry updateDTA, in DTAEntry upgradeDTA)
        {
            string? location = null;
            float[]? volumes = null;
            float[]? pans = null;
            float[]? cores = null;

            ParseDTA(entry, in baseDTA, ref location, ref volumes, ref pans, ref cores);
            ParseDTA(entry, in upgradeDTA, ref location, ref volumes, ref pans, ref cores);
            ParseDTA(entry, in updateDTA, ref location, ref volumes, ref pans, ref cores);

            if (entry._metadata.Name.Length == 0)
            {
                return new ScanUnexpected(ScanResult.NoName);
            }

            if (location == null || pans == null || volumes == null || cores == null)
            {
                return new ScanUnexpected(ScanResult.DTAError);
            }

            entry._parsedYear = entry._metadata.Year;

            unsafe
            {
                var usedIndices = stackalloc bool[pans.Length];
                float[] CalculateStemValues(int[] indices)
                {
                    float[] values = new float[2 * indices.Length];
                    for (int i = 0; i < indices.Length; i++)
                    {
                        float theta = (pans[indices[i]] + 1) * ((float) Math.PI / 4);
                        float volRatio = (float) Math.Pow(10, volumes[indices[i]] / 20);
                        values[2 * i] = volRatio * (float) Math.Cos(theta);
                        values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                        usedIndices[indices[i]] = true;
                    }
                    return values;
                }

                if (entry._indices.Drums.Length > 0)
                {
                    entry._panning.Drums = CalculateStemValues(entry._indices.Drums);
                }

                if (entry._indices.Bass.Length > 0)
                {
                    entry._panning.Bass = CalculateStemValues(entry._indices.Bass);
                }

                if (entry._indices.Guitar.Length > 0)
                {
                    entry._panning.Guitar = CalculateStemValues(entry._indices.Guitar);
                }

                if (entry._indices.Keys.Length > 0)
                {
                    entry._panning.Keys = CalculateStemValues(entry._indices.Keys);
                }

                if (entry._indices.Vocals.Length > 0)
                {
                    entry._panning.Vocals = CalculateStemValues(entry._indices.Vocals);
                }

                if (entry._indices.Crowd.Length > 0)
                {
                    entry._panning.Crowd = CalculateStemValues(entry._indices.Crowd);
                }

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
                    entry._indices.Track = leftover.ToArray();
                    entry._panning.Track = CalculateStemValues(entry._indices.Track);
                }
            }

            if (entry._rbIntensities.FourLaneDrums > -1)
            {
                entry._parts.FourLaneDrums.Intensity = (sbyte)GetIntensity(entry._rbIntensities.FourLaneDrums, DrumDiffMap);
                if (entry._parts.ProDrums.Intensity == -1)
                {
                    entry._parts.ProDrums.Intensity = entry._parts.FourLaneDrums.Intensity;
                }
            }
            if (entry._rbIntensities.FiveFretGuitar > -1)
            {
                entry._parts.FiveFretGuitar.Intensity = (sbyte)GetIntensity(entry._rbIntensities.FiveFretGuitar, GuitarDiffMap);
                if (entry._parts.ProGuitar_17Fret.Intensity == -1)
                {
                    entry._parts.ProGuitar_22Fret.Intensity = entry._parts.ProGuitar_17Fret.Intensity = entry._parts.FiveFretGuitar.Intensity;
                }
            }
            if (entry._rbIntensities.FiveFretBass > -1)
            {
                entry._parts.FiveFretBass.Intensity = (sbyte)GetIntensity(entry._rbIntensities.FiveFretBass, GuitarDiffMap);
                if (entry._parts.ProBass_17Fret.Intensity == -1)
                {
                    entry._parts.ProBass_22Fret.Intensity = entry._parts.ProBass_17Fret.Intensity = entry._parts.FiveFretGuitar.Intensity;
                }
            }
            if (entry._rbIntensities.LeadVocals > -1)
            {
                entry._parts.LeadVocals.Intensity = (sbyte)GetIntensity(entry._rbIntensities.LeadVocals, GuitarDiffMap);
                if (entry._parts.HarmonyVocals.Intensity == -1)
                {
                    entry._parts.HarmonyVocals.Intensity = entry._parts.LeadVocals.Intensity;
                }
            }
            if (entry._rbIntensities.Keys > -1)
            {
                entry._parts.Keys.Intensity = (sbyte)GetIntensity(entry._rbIntensities.Keys, GuitarDiffMap);
                if (entry._parts.ProKeys.Intensity == -1)
                {
                    entry._parts.ProKeys.Intensity = entry._parts.Keys.Intensity;
                }
            }
            if (entry._rbIntensities.ProGuitar > -1)
            {
                entry._parts.ProGuitar_17Fret.Intensity = (sbyte)GetIntensity(entry._rbIntensities.ProGuitar, RealGuitarDiffMap);
                entry._parts.ProGuitar_22Fret.Intensity = entry._parts.ProGuitar_17Fret.Intensity;
                if (entry._parts.FiveFretGuitar.Intensity == -1)
                {
                    entry._parts.FiveFretGuitar.Intensity = entry._parts.ProGuitar_17Fret.Intensity;
                }
            }
            if (entry._rbIntensities.ProBass > -1)
            {
                entry._parts.ProBass_17Fret.Intensity = (sbyte)GetIntensity(entry._rbIntensities.ProBass, RealGuitarDiffMap);
                entry._parts.ProBass_22Fret.Intensity = entry._parts.ProBass_17Fret.Intensity;
                if (entry._parts.FiveFretBass.Intensity == -1)
                {
                    entry._parts.FiveFretBass.Intensity = entry._parts.ProBass_17Fret.Intensity;
                }
            }
            if (entry._rbIntensities.ProKeys > -1)
            {
                entry._parts.ProKeys.Intensity = (sbyte)GetIntensity(entry._rbIntensities.ProKeys, RealKeysDiffMap);
                if (entry._parts.Keys.Intensity == -1)
                {
                    entry._parts.Keys.Intensity = entry._parts.ProKeys.Intensity;
                }
            }
            if (entry._rbIntensities.ProDrums > -1)
            {
                entry._parts.ProDrums.Intensity = (sbyte)GetIntensity(entry._rbIntensities.ProDrums, DrumDiffMap);
                if (entry._parts.FourLaneDrums.Intensity == -1)
                {
                    entry._parts.FourLaneDrums.Intensity = entry._parts.ProDrums.Intensity;
                }
            }
            if (entry._rbIntensities.HarmonyVocals > -1)
            {
                entry._parts.HarmonyVocals.Intensity = (sbyte)GetIntensity(entry._rbIntensities.HarmonyVocals, DrumDiffMap);
                if (entry._parts.LeadVocals.Intensity == -1)
                {
                    entry._parts.LeadVocals.Intensity = entry._parts.HarmonyVocals.Intensity;
                }
            }
            if (entry._rbIntensities.Band > -1)
            {
                entry._parts.BandDifficulty.Intensity = (sbyte)GetIntensity(entry._rbIntensities.Band, BandDiffMap);
                entry._parts.BandDifficulty.SubTracks = 1;
            }
            return location;
        }

        protected static ScanResult ScanMidis(RBCONEntry entry, in FixedArray<byte> mainMidi)
        {
            var updateMidi = FixedArray<byte>.Null;
            var upgradeMidi = FixedArray<byte>.Null;
            try
            {
                if (entry._upgrade != null)
                {
                    upgradeMidi = entry._upgrade.LoadUpgradeMidi();
                    if (!upgradeMidi.IsAllocated)
                    {
                        throw new FileNotFoundException("Upgrade midi not located");
                    }
                }

                if (entry._updateMidiLastWrite.HasValue)
                {
                    string updateFile = Path.Combine(entry._updateDirectoryAndDtaLastWrite!.Value.FullName, entry._nodeName, entry._nodeName + "_update.mid");
                    updateMidi = FixedArray.LoadFile(updateFile);
                }

                var drumsType = DrumsType.ProDrums;

                long bufLength = mainMidi.Length;
                if (updateMidi.IsAllocated)
                {
                    var updateResult = ParseMidi(in updateMidi, ref entry._parts, ref drumsType);
                    switch (updateResult.Error)
                    {
                        case ScanResult.InvalidResolution:      return ScanResult.InvalidResolution_Update;
                        case ScanResult.MultipleMidiTrackNames: return ScanResult.MultipleMidiTrackNames_Update;
                    }
                    bufLength += updateMidi.Length;
                }

                if (upgradeMidi.IsAllocated)
                {
                    var upgradeResult = ParseMidi(in upgradeMidi, ref entry._parts, ref drumsType);
                    switch (upgradeResult.Error)
                    {
                        case ScanResult.InvalidResolution:      return ScanResult.InvalidResolution_Upgrade;
                        case ScanResult.MultipleMidiTrackNames: return ScanResult.MultipleMidiTrackNames_Upgrade;
                    }
                    bufLength += upgradeMidi.Length;
                }

                var resolution = ParseMidi(in mainMidi, ref entry._parts, ref drumsType);
                if (!resolution)
                {
                    return resolution.Error;
                }

                if (!IsValid(in entry._parts))
                {
                    return ScanResult.NoNotes;
                }

                entry._parts.ProDrums.Difficulties = entry._parts.FourLaneDrums.Difficulties;
                entry._settings.SustainCutoffThreshold = resolution.Value / 3;
                if (entry._settings.HopoThreshold == -1)
                {
                    entry._settings.HopoThreshold = entry._settings.SustainCutoffThreshold;
                }

                using var buffer = FixedArray<byte>.Alloc(bufLength);
                unsafe
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr, mainMidi.Ptr, (uint) mainMidi.Length);

                    long offset = mainMidi.Length;
                    if (updateMidi.IsAllocated)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, updateMidi.Ptr, (uint) updateMidi.Length);
                        offset += updateMidi.Length;
                        updateMidi.Dispose();
                    }

                    if (upgradeMidi.IsAllocated)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, upgradeMidi.Ptr, (uint) upgradeMidi.Length);
                        upgradeMidi.Dispose();
                    }
                }
                entry._hash = HashWrapper.Hash(buffer.ReadOnlySpan);
                return ScanResult.Success;
            }
            catch (Exception ex)
            {
                if (updateMidi.IsAllocated)
                {
                    updateMidi.Dispose();
                }

                if (upgradeMidi.IsAllocated)
                {
                    upgradeMidi.Dispose();
                }
                YargLogger.LogException(ex);
                return ScanResult.PossibleCorruption;
            }
        }

        protected Stream? LoadUpdateMoggStream()
        {
            Stream? stream = null;
            if (_updateDirectoryAndDtaLastWrite.HasValue)
            {
                string updateMoggPath = Path.Combine(_updateDirectoryAndDtaLastWrite.Value.FullName, _subName, _subName + "_update.mogg");
                if (File.Exists(updateMoggPath))
                {
                    stream = File.OpenRead(updateMoggPath);
                }
            }
            return stream;
        }

        protected YARGImage LoadUpdateAlbumData()
        {
            var image = YARGImage.Null;
            if (_updateDirectoryAndDtaLastWrite.HasValue)
            {
                string updateImgPath = Path.Combine(_updateDirectoryAndDtaLastWrite.Value.FullName, _subName, "gen", _subName + "_keep.png_xbox");
                if (File.Exists(updateImgPath))
                {
                    image = YARGImage.LoadDXT(updateImgPath);
                }
            }
            return image;
        }

        protected FixedArray<byte> LoadUpdateMiloData()
        {
            var data = FixedArray<byte>.Null;
            if (_updateDirectoryAndDtaLastWrite.HasValue)
            {
                string updateMiloPath = Path.Combine(_updateDirectoryAndDtaLastWrite.Value.FullName, _subName, "gen", _subName + ".milo_xbox");
                if (File.Exists(updateMiloPath))
                {
                    data = FixedArray.LoadFile(updateMiloPath);
                }
            }
            return data;
        }

        private static void WriteUpdateInfo(in AbridgedFileInfo? info, MemoryStream stream)
        {
            stream.Write(info != null);
            if (info != null)
            {
                stream.Write(info.Value.FullName);
            }
        }

        private static void WriteArray<TType>(in TType[] values, MemoryStream stream)
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

        private static void WriteArray(in string[] strings, MemoryStream stream)
        {
            stream.Write(strings.Length, Endianness.Little);
            for (int i = 0; i < strings.Length; ++i)
            {
                stream.Write(strings[i]);
            }
        }

        private static TType[] ReadArray<TType>(ref FixedArrayStream stream)
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

        private static string[] ReadStringArray(ref FixedArrayStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<string>();
            }

            var strings = new string[length];
            for (int i = 0; i < strings.Length; ++i)
            {
                strings[i] = stream.ReadString();
            }
            return strings;
        }

        private static void ReadAudio<TType>(ref RBAudio<TType> audio, ref FixedArrayStream stream)
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

        private static void WriteAudio<TType>(in RBAudio<TType> audio, MemoryStream stream)
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

        private static void ParseDTA(RBCONEntry entry, in DTAEntry dta, ref string? location, ref float[]? volumes, ref float[]? pans, ref float[]? cores)
        {
            if (dta.Name != null)    { entry._metadata.Name    = YARGDTAReader.DecodeString(dta.Name.Value, dta.MetadataEncoding); }
            if (dta.Artist != null)  { entry._metadata.Artist  = YARGDTAReader.DecodeString(dta.Artist.Value, dta.MetadataEncoding); }
            if (dta.Album != null)   { entry._metadata.Album   = YARGDTAReader.DecodeString(dta.Album.Value, dta.MetadataEncoding); }
            if (dta.Charter != null) { entry._metadata.Charter = dta.Charter; }
            if (dta.Genre != null)   { entry._metadata.Genre   = dta.Genre; }
            if (dta.YearAsNumber != null)
            {
                entry._yearAsNumber = dta.YearAsNumber.Value;
                entry._metadata.Year = entry._yearAsNumber.ToString("D4");
            }
            if (dta.Source != null)
            {
                if (!entry._nodeName.StartsWith("UGC_") && (dta.Source == "ugc" || dta.Source == "ugc_plus" || (dta.Source == "rb2" && dta.UGC.HasValue && dta.UGC.Value)))
                {
                    entry._metadata.Source = "customs";
                }
                else
                {
                    entry._metadata.Source = dta.Source;
                }
            }
            if (dta.Playlist != null)             { entry._metadata.Playlist      = dta.Playlist; }
            if (dta.SongLength != null)           { entry._metadata.SongLength    = dta.SongLength.Value; }
            if (dta.IsMaster != null)             { entry._metadata.IsMaster      = dta.IsMaster.Value; }
            if (dta.AlbumTrack != null)           { entry._metadata.AlbumTrack    = dta.AlbumTrack.Value; }
            if (dta.Preview != null)              { entry._metadata.Preview       = dta.Preview.Value; }
            if (dta.HopoThreshold != null)        { entry._settings.HopoThreshold = dta.HopoThreshold.Value; }
            if (dta.SongRating != null)           { entry._metadata.SongRating    = dta.SongRating.Value; }

            if (dta.VocalPercussionBank != null)  { entry._rbMetadata.VocalPercussionBank  = dta.VocalPercussionBank; }
            if (dta.VocalGender != null)          { entry._rbMetadata.VocalGender          = dta.VocalGender.Value; }
            if (dta.VocalSongScrollSpeed != null) { entry._rbMetadata.VocalSongScrollSpeed = dta.VocalSongScrollSpeed.Value; }
            if (dta.VocalTonicNote != null)       { entry._rbMetadata.VocalTonicNote       = dta.VocalTonicNote.Value; }
            if (dta.VideoVenues != null)          { entry._rbMetadata.VideoVenues          = dta.VideoVenues; }
            if (dta.DrumBank != null)             { entry._rbMetadata.DrumBank             = dta.DrumBank; }
            if (dta.SongID != null)               { entry._rbMetadata.SongID               = dta.SongID; }
            if (dta.SongTonality != null)         { entry._rbMetadata.SongTonality         = dta.SongTonality.Value; }
            if (dta.Soloes != null)               { entry._rbMetadata.Soloes               = dta.Soloes; }
            if (dta.AnimTempo != null)            { entry._rbMetadata.AnimTempo            = dta.AnimTempo.Value; }
            if (dta.TuningOffsetCents != null)    { entry._rbMetadata.TuningOffsetCents    = dta.TuningOffsetCents.Value; }
            if (dta.RealGuitarTuning != null)     { entry._rbMetadata.RealGuitarTuning     = dta.RealGuitarTuning; }
            if (dta.RealBassTuning != null)       { entry._rbMetadata.RealBassTuning       = dta.RealBassTuning; }

            if (dta.Cores != null)   { cores = dta.Cores; }
            if (dta.Volumes != null) { volumes = dta.Volumes; }
            if (dta.Pans != null)    { pans = dta.Pans; }

            if (dta.Location != null) { location = dta.Location; }

            if (dta.Indices != null)  { entry._indices = dta.Indices.Value; }

            if (dta.CrowdChannels != null) { entry._indices.Crowd = dta.CrowdChannels; }

            if (dta.Intensities.Band >= 0)           { entry._rbIntensities.Band           = dta.Intensities.Band; }
            if (dta.Intensities.FiveFretGuitar >= 0) { entry._rbIntensities.FiveFretGuitar = dta.Intensities.FiveFretGuitar; }
            if (dta.Intensities.FiveFretBass >= 0)   { entry._rbIntensities.FiveFretBass   = dta.Intensities.FiveFretBass; }
            if (dta.Intensities.FiveFretRhythm >= 0) { entry._rbIntensities.FiveFretRhythm = dta.Intensities.FiveFretRhythm; }
            if (dta.Intensities.FiveFretCoop >= 0)   { entry._rbIntensities.FiveFretCoop   = dta.Intensities.FiveFretCoop; }
            if (dta.Intensities.Keys >= 0)           { entry._rbIntensities.Keys           = dta.Intensities.Keys; }
            if (dta.Intensities.FourLaneDrums >= 0)  { entry._rbIntensities.FourLaneDrums  = dta.Intensities.FourLaneDrums; }
            if (dta.Intensities.ProDrums >= 0)       { entry._rbIntensities.ProDrums       = dta.Intensities.ProDrums; }
            if (dta.Intensities.ProGuitar >= 0)      { entry._rbIntensities.ProGuitar      = dta.Intensities.ProGuitar; }
            if (dta.Intensities.ProBass >= 0)        { entry._rbIntensities.ProBass        = dta.Intensities.ProBass; }
            if (dta.Intensities.ProKeys >= 0)        { entry._rbIntensities.ProKeys        = dta.Intensities.ProKeys; }
            if (dta.Intensities.LeadVocals >= 0)     { entry._rbIntensities.LeadVocals     = dta.Intensities.LeadVocals; }
            if (dta.Intensities.HarmonyVocals >= 0)  { entry._rbIntensities.HarmonyVocals  = dta.Intensities.HarmonyVocals; }
        }

        private static int GetIntensity(int rank, int[] values)
        {
            int intensity = 0;
            while (intensity < 6 && values[intensity] <= rank)
            {
                ++intensity;
            }
            return intensity;
        }
    }
}
