using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class PackedRBCONEntry : RBCONEntry
    {
        private readonly CONFileListing? _midiListing;
        private readonly CONFileListing? _moggListing;
        private readonly CONFileListing? _miloListing;
        private readonly CONFileListing? _imgListing;
        private readonly DateTime _lastMidiUpdate;

        protected override DateTime MidiLastWrite => _midiListing?.ConFile.LastUpdatedTime ?? DateTime.MinValue;

        public override string Directory { get; }
        public override EntryType SubType => EntryType.CON;

        public static (ScanResult, PackedRBCONEntry?) ProcessNewEntry(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                var song = new PackedRBCONEntry(group, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(group.CONFile);
                if (result != ScanResult.Success)
                {
                    return (result, null);
                }
                return (result, song);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, null);
                return (ScanResult.DTAError, null);
            }
        }

        public static PackedRBCONEntry? TryLoadFromCache(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string psuedoDirectory = reader.ReadString();

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

            var metadata = new SongMetadata(reader, strings);
            var song = new PackedRBCONEntry(midiListing, midiLastWrite, moggListing, miloListing, imgListing, psuedoDirectory, updateMidi, metadata, reader);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        public static PackedRBCONEntry LoadFromCache_Quick(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string psuedoDirectory = reader.ReadString();

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

            var metadata = new SongMetadata(reader, strings);
            var song = new PackedRBCONEntry(midiListing, midiLastWrite, moggListing, miloListing, imgListing, psuedoDirectory, updateMidi, metadata, reader);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                song.Upgrade = upgrade.Item2;
            }
            return song;
        }

        private PackedRBCONEntry(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
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

        private PackedRBCONEntry(CONFileListing? midi, DateTime midiLastWrite, CONFileListing? moggListing, CONFileListing? miloListing, CONFileListing? imgListing, string directory,
            AbridgedFileInfo? updateMidi, in SongMetadata metadata, BinaryReader reader)
            : base(updateMidi, metadata, reader)
        {
            _midiListing = midi;
            _moggListing = moggListing;
            _miloListing = miloListing;
            _imgListing = imgListing;
            _lastMidiUpdate = midiLastWrite;
            Directory = directory;
        }

        protected override void SerializeSubData(BinaryWriter writer)
        {
            writer.Write(Directory);
            writer.Write(_midiListing!.Filename);
            writer.Write(_midiListing.lastWrite.ToBinary());
            writer.Write(UpdateMidi != null);
            UpdateMidi?.Serialize(writer);
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            if (_midiListing == null)
            {
                return null;
            }

            string actualDirectory = Path.GetDirectoryName(_midiListing.ConFile.FullName);

            // Unlike other entry types, you can't assign venues to specific songs
            // As a solution, instead, let users place a bunch of venues in the same folder and randomly select one
            var venue = SelectRandomYarground(actualDirectory);
            if (venue != null)
            {
                return venue;
            }

            foreach (var name in BACKGROUND_FILENAMES)
            {
                var fileBase = Path.Combine(actualDirectory, name);
                foreach (var ext in VIDEO_EXTENSIONS)
                {
                    string backgroundPath = fileBase + ext;
                    if (File.Exists(backgroundPath))
                    {
                        var stream = File.OpenRead(backgroundPath);
                        return new BackgroundResult(BackgroundType.Video, stream);
                    }
                }
            }

            foreach (var name in BACKGROUND_FILENAMES)
            {
                var fileBase = Path.Combine(actualDirectory, name);
                foreach (var ext in IMAGE_EXTENSIONS)
                {
                    string backgroundPath = fileBase + ext;
                    if (File.Exists(backgroundPath))
                    {
                        var stream = File.OpenRead(backgroundPath);
                        return new BackgroundResult(BackgroundType.Image, stream);
                    }
                }
            }

            return null;
        }

        public override byte[]? LoadMiloData()
        {
            if (UpdateMilo != null && UpdateMilo.Exists())
            {
                return File.ReadAllBytes(UpdateMilo.FullName);
            }
            return _miloListing?.LoadAllBytes();
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

        protected override byte[]? LoadRawImageData()
        {
            return base.LoadRawImageData() ?? _imgListing?.LoadAllBytes();
        }

        protected override Stream? GetMoggStream()
        {
            return base.GetMoggStream() ?? _moggListing?.CreateStream();
        }

        protected override bool IsMoggValid(CONFile? file)
        {
            using var stream = base.GetMoggStream();
            if (stream != null)
            {
                int version = stream.Read<int>(Endianness.Little);
                return version == 0x0A || version == 0xf0;
            }
            return _moggListing != null && CONFileListing.GetMoggVersion(_moggListing, file!) == 0x0A;
        }
    }
}
