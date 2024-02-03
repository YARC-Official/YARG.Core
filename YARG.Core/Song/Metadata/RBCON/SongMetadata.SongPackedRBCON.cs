using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class PackedRBCONMetadata : RBCONSubMetadata
    {
        private readonly CONFileListing? _midiListing;
        private readonly CONFileListing? _moggListing;
        private readonly CONFileListing? _miloListing;
        private readonly CONFileListing? _imgListing;
        private readonly DateTime _lastMidiUpdate;

        public override string Directory { get; } 
        protected override DateTime MidiLastWrite => _midiListing?.ConFile.LastUpdatedTime ?? DateTime.MinValue;

        public static (ScanResult, PackedRBCONMetadata?) ProcessNewEntry(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                var song = new PackedRBCONMetadata(group, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(group.CONFile);
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

        public static PackedRBCONMetadata? TryLoadFromCache(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string midiFilename = reader.ReadString();
            var midiListing = file.TryGetListing(midiFilename);
            if (midiListing == null)
            {
                return null;
            }

            var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (midiListing.lastWrite != midiLastWrite)
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

            var moggListing = file.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"));

            if (!midiFilename.StartsWith($"songs/{nodename}"))
                nodename = midiFilename.Split('/')[1];

            string genPath = $"songs/{nodename}/gen/{nodename}";
            var miloListing = file.TryGetListing(genPath + ".milo_xbox");
            var imgListing = file.TryGetListing(genPath + "_keep.png_xbox");

            var song = new PackedRBCONMetadata(midiListing, midiLastWrite, moggListing, miloListing, imgListing, updateMidi, reader, strings);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        public static PackedRBCONMetadata LoadFromCache_Quick(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string midiFilename = reader.ReadString();
            var midiListing = file.TryGetListing(midiFilename);
            var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());

            var updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

            var moggListing = file.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"));

            if (!midiFilename.StartsWith($"songs/{nodename}"))
                nodename = midiFilename.Split('/')[1];

            string genPath = $"songs/{nodename}/gen/{nodename}";
            var miloListing = file.TryGetListing(genPath + ".milo_xbox");
            var imgListing = file.TryGetListing(genPath + "_keep.png_xbox");

            var song = new PackedRBCONMetadata(midiListing, midiLastWrite, moggListing, miloListing, imgListing, updateMidi, reader, strings);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        private PackedRBCONMetadata(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            var results = Init(nodeName, reader, updates, upgrades, group.DefaultPlaylist);

            string midiPath = results.location + ".mid";
            _midiListing = group.CONFile.TryGetListing(midiPath);
            if (_midiListing == null)
                throw new Exception($"Required midi file '{midiPath}' was not located");
            _lastMidiUpdate = _midiListing.lastWrite;

            _moggListing = group.CONFile.TryGetListing(results.location + ".mogg");

            if (!results.location.StartsWith($"songs/{nodeName}"))
                nodeName = _midiListing.Filename.Split('/')[1];

            string genPath = $"songs/{nodeName}/gen/{nodeName}";
            _miloListing = group.CONFile.TryGetListing(genPath + ".milo_xbox");
            _imgListing = group.CONFile.TryGetListing(genPath + "_keep.png_xbox");

            string midiDirectory = group.CONFile.Listings[_midiListing.pathIndex].Filename;
            Directory = Path.Combine(group.Location, midiDirectory);
        }

        private PackedRBCONMetadata(CONFileListing? midi, DateTime midiLastWrite, CONFileListing? moggListing, CONFileListing? miloListing, CONFileListing? imgListing
            , AbridgedFileInfo? updateMidi, BinaryReader reader, CategoryCacheStrings strings)
            : base(updateMidi, reader, strings)
        {
            Directory = reader.ReadString();

            _midiListing = midi;
            _moggListing = moggListing;
            _miloListing = miloListing;
            _imgListing = imgListing;
            _lastMidiUpdate = midiLastWrite;
        }

        public override void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(_midiListing!.Filename);
            writer.Write(_midiListing.lastWrite.ToBinary());
            base.Serialize(writer, node);
            writer.Write(Directory);
        }

        protected override Stream? GetMidiStream()
        {
            if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiUpdate))
                return null;
            return _midiListing.CreateStream();
        }

        protected override byte[]? LoadMidiFile(CONFile? file)
        {
            if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiUpdate))
            {
                return null;
            }
            return _midiListing.LoadAllBytes(file!);
        }

        protected override byte[]? LoadMiloFile()
        {
            if (UpdateMilo != null && UpdateMilo.Exists())
            {
                return File.ReadAllBytes(UpdateMilo.FullName);
            }
            return _miloListing?.LoadAllBytes();
        }

        protected override byte[]? LoadImgFile()
        {
            if (UpdateImage != null && UpdateImage.Exists())
            {
                return File.ReadAllBytes(UpdateImage.FullName);
            }
            return _imgListing?.LoadAllBytes();
        }

        protected override Stream? GetMoggStream()
        {
            var stream = LoadUpdateMoggStream();
            if (stream != null)
                return stream;
            return _moggListing?.CreateStream();
        }

        protected override bool IsMoggValid(CONFile? file)
        {
            using var stream = LoadUpdateMoggStream();
            if (stream != null)
            {
                int version = stream.Read<int>(Endianness.Little);
                return version == 0x0A || version == 0xf0;
            }
            return _moggListing != null && CONFileListing.GetMoggVersion(_moggListing, file!) == 0x0A;
        }
    }
}
