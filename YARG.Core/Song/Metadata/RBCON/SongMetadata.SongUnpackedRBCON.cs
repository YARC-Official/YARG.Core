using System;
using System.IO;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class UnpackedRBCONMetadata : RBCONSubMetadata
    {
        private readonly AbridgedFileInfo? _dta;
        private readonly AbridgedFileInfo _midi;
        private readonly string _nodename;

        public override string Directory { get; }
        protected override DateTime MidiLastWrite => _midi.LastUpdatedTime;

        public override EntryType SubType => EntryType.ExCON;

        public static (ScanResult, UnpackedRBCONMetadata?) ProcessNewEntry(UnpackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                var song = new UnpackedRBCONMetadata(group, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(null);
                if (result != ScanResult.Success)
                {
                    return (result, null);
                }
                return (result, song);
            }
            catch (Exception ex)
            {
                YargTrace.LogError(ex.Message);
                return (ScanResult.DTAError, null);
            }
        }

        public static UnpackedRBCONMetadata? TryLoadFromCache(string directory, AbridgedFileInfo dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string subname = reader.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = AbridgedFileInfo.TryParseInfo(midiPath, reader);
            if (midiInfo == null)
            {
                return null;
            }

            AbridgedFileInfo? updateMidi = null;
            if (reader.ReadBoolean())
            {
                updateMidi = AbridgedFileInfo.TryParseInfo(reader, false);
                if (updateMidi == null)
                {
                    return null;
                }
            }

            var song = new UnpackedRBCONMetadata(songDirectory, subname, dta, midiInfo, updateMidi, reader, strings);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        public static UnpackedRBCONMetadata LoadFromCache_Quick(string directory, AbridgedFileInfo? dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string subname = reader.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = new AbridgedFileInfo(midiPath, reader);

            var updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

            var song = new UnpackedRBCONMetadata(songDirectory, subname, dta, midiInfo, updateMidi, reader, strings);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        private UnpackedRBCONMetadata(UnpackedCONGroup group, string nodename, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            var results = Init(nodename, reader, updates, upgrades, group.DefaultPlaylist);
            

            if (!results.location.StartsWith($"songs/" + nodename))
                nodename = results.location.Split('/')[1];
            _nodename = nodename;

            Directory = Path.Combine(group.Location, nodename);
            string midiPath = Path.Combine(Directory, nodename + ".mid");

            FileInfo midiInfo = new(midiPath);
            if (!midiInfo.Exists)
                throw new Exception($"Required midi file '{midiPath}' was not located");

            _midi = new AbridgedFileInfo(midiInfo);
            _dta = group.DTA;
        }

        private UnpackedRBCONMetadata(string directory, string nodename, AbridgedFileInfo? dta, AbridgedFileInfo midi
            , AbridgedFileInfo? updateMidi, BinaryReader reader, CategoryCacheStrings strings)
            : base(updateMidi, reader, strings)
        {
            Directory = directory;
            _nodename = nodename;
            _dta = dta;
            _midi = midi;
        }

        public override void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(_nodename);
            writer.Write(_midi.LastUpdatedTime.ToBinary());
            base.Serialize(writer, node);
        }

        public override byte[]? LoadMiloData()
        {
            if (UpdateMilo != null && UpdateMilo.Exists())
            {
                return File.ReadAllBytes(UpdateMilo.FullName);
            }

            string milo = Path.Combine(Directory, "gen", _nodename + ".milo_xbox");
            if (!File.Exists(milo))
            {
                return null;
            }
            return File.ReadAllBytes(milo);
        }

        protected override Stream? GetMidiStream()
        {
            if (_dta == null || !_dta.IsStillValid() || !_midi.IsStillValid())
            {
                return null;
            }
            return new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override byte[]? LoadMidiFile(CONFile? _)
        {
            if (_dta == null || !_dta.IsStillValid() || !_midi.IsStillValid())
            {
                return null;
            }
            return File.ReadAllBytes(_midi.FullName);
        }

        protected override byte[]? LoadRawImageData()
        {
            if (UpdateImage != null && UpdateImage.Exists())
            {
                return File.ReadAllBytes(UpdateImage.FullName);
            }

            string image = Path.Combine(Directory, "gen", _nodename + "_keep.png_xbox");
            if (!File.Exists(image))
            {
                return null;
            }
            return File.ReadAllBytes(image);
        }

        protected override Stream? GetMoggStream()
        {
            var stream = LoadUpdateMoggStream();
            if (stream != null)
            {
                return stream;
            }

            string path = Path.Combine(Directory, _nodename + ".yarg_mogg");
            if (File.Exists(path))
            {
                return new YargMoggReadStream(path);
            }

            path = Path.Combine(Directory, _nodename + ".mogg");
            if (!File.Exists(path))
            {
                return null;
            }
            return new FileStream(path, FileMode.Open, FileAccess.Read);
        }

        protected override bool IsMoggValid(CONFile? _)
        {
            using var stream = GetMoggStream();
            if (stream == null)
            {
                return false;
            }

            int version = stream.Read<int>(Endianness.Little);
            return version == 0x0A || version == 0xf0;
        }
    }
}
