﻿using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Logging;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song
{
    public sealed class PackedRBCONEntry : RBCONEntry
    {
        private readonly CONFileListing? _midiListing;
        private readonly CONFileListing? _moggListing;
        private readonly CONFileListing? _miloListing;
        private readonly CONFileListing? _imgListing;
        private readonly DateTime _lastMidiWrite;

        protected override DateTime MidiLastUpdate => _midiListing?.ConFile.LastUpdatedTime ?? DateTime.MinValue;
        public override string Directory { get; } = string.Empty;
        public override EntryType SubType => EntryType.CON;

        public static (ScanResult, PackedRBCONEntry?) ProcessNewEntry(PackedCONGroup group, string nodename, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                var song = new PackedRBCONEntry(group, nodename, reader, updates, upgrades);
                if (song._midiListing == null)
                {
                    YargLogger.LogFormatError("Required midi file for {0} - {1} was not located", group.Info.FullName, item2: nodename);
                    return (ScanResult.MissingMidi, null);
                }

                var result = song.ParseRBCONMidi(group.Stream);
                if (result != ScanResult.Success)
                {
                    return (result, null);
                }
                return (result, song);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, null);
                return (ScanResult.DTAError, null);
            }
        }

        public static PackedRBCONEntry? TryLoadFromCache(in CONFile conFile, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var psuedoDirectory = reader.ReadString();

            string midiFilename = reader.ReadString();
            if (!conFile.TryGetListing(midiFilename, out var midiListing))
            {
                return null;
            }

            var lastMidiWrite = DateTime.FromBinary(reader.ReadInt64());
            if (midiListing.LastWrite != lastMidiWrite)
            {
                return null;
            }

            AbridgedFileInfo_Length? updateMidi = null;
            if (reader.ReadBoolean())
            {
                updateMidi = AbridgedFileInfo_Length.TryParseInfo(reader, false);
                if (updateMidi == null)
                {
                    return null;
                }
            }

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Item2 : null;

            conFile.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"), out var moggListing);

            if (!midiFilename.StartsWith($"songs/{nodename}"))
                nodename = midiFilename.Split('/')[1];

            string genPath = $"songs/{nodename}/gen/{nodename}";
            conFile.TryGetListing(genPath + ".milo_xbox", out var miloListing);
            conFile.TryGetListing(genPath + "_keep.png_xbox", out var imgListing);
            return new PackedRBCONEntry(midiListing, lastMidiWrite, moggListing, miloListing, imgListing, psuedoDirectory, updateMidi, upgrade, reader, strings);
        }

        public static PackedRBCONEntry LoadFromCache_Quick(in CONFile conFile, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var psuedoDirectory = reader.ReadString();

            string midiFilename = reader.ReadString();
            conFile.TryGetListing(midiFilename, out var midiListing);
            var lastMidiWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo_Length? updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo_Length(reader) : null;
            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Item2 : null;

            conFile.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"), out var moggListing);

            if (!midiFilename.StartsWith($"songs/{nodename}"))
                nodename = midiFilename.Split('/')[1];

            string genPath = $"songs/{nodename}/gen/{nodename}";
            conFile.TryGetListing(genPath + ".milo_xbox", out var miloListing);
            conFile.TryGetListing(genPath + "_keep.png_xbox", out var imgListing);
            return new PackedRBCONEntry(midiListing, lastMidiWrite, moggListing, miloListing, imgListing, psuedoDirectory, updateMidi, upgrade, reader, strings);
        }

        private PackedRBCONEntry(PackedCONGroup group, string nodename, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
            : base()
        {
            var results = Init(nodename, reader, updates, upgrades, group.DefaultPlaylist);
            string midiPath = results.location + ".mid";
            if (!group.ConFile.TryGetListing(midiPath, out _midiListing))
            {
                return;
            }

            _lastMidiWrite = _midiListing.LastWrite;

            group.ConFile.TryGetListing(results.location + ".mogg", out _moggListing);

            if (!results.location.StartsWith($"songs/{nodename}"))
                nodename = _midiListing.Filename.Split('/')[1];

            string genPath = $"songs/{nodename}/gen/{nodename}";
            group.ConFile.TryGetListing(genPath + ".milo_xbox", out _miloListing);
            group.ConFile.TryGetListing(genPath + "_keep.png_xbox", out _imgListing);

            string midiDirectory = group.ConFile.GetFilename(_midiListing.PathIndex);
            Directory = Path.Combine(group.Location, midiDirectory);
        }

        private PackedRBCONEntry(CONFileListing? midi, DateTime midiLastWrite, CONFileListing? moggListing, CONFileListing? miloListing, CONFileListing? imgListing, string directory,
            AbridgedFileInfo_Length? updateMidi, IRBProUpgrade? upgrade, BinaryReader reader, CategoryCacheStrings strings)
            : base(updateMidi, upgrade, reader, strings)
        {
            _midiListing = midi;
            _moggListing = moggListing;
            _miloListing = miloListing;
            _imgListing = imgListing;
            _lastMidiWrite = midiLastWrite;

            Directory = directory;
        }

        public override void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(Directory);
            writer.Write(_midiListing!.Filename);
            writer.Write(_midiListing.LastWrite.ToBinary());
            base.Serialize(writer, node);
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            if (_midiListing == null)
            {
                return null;
            }

            string actualDirectory = Path.GetDirectoryName(_midiListing.ConFile.FullName);
            string nodename = _midiListing.Filename.Split('/')[1];
            if ((options & BackgroundType.Yarground) > 0)
            {
                string specifcVenue = Path.Combine(actualDirectory, nodename + YARGROUND_EXTENSION);
                if (File.Exists(specifcVenue))
                {
                    var stream = File.OpenRead(specifcVenue);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }

                var venues = System.IO.Directory.EnumerateFiles(actualDirectory)
                    .Where(file => Path.GetExtension(file) == YARGROUND_EXTENSION)
                    .ToArray();

                if (venues.Length > 0)
                {
                    var stream = File.OpenRead(venues[BACKROUND_RNG.Next(venues.Length)]);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                string[] filenames = { nodename, "bg", "background", "video" };
                foreach (var name in filenames)
                {
                    string fileBase = Path.Combine(actualDirectory, name);
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
            }

            if ((options & BackgroundType.Image) > 0)
            {
                string[] filenames = { nodename, "bg", "background" };
                foreach (var name in filenames)
                {
                    var fileBase = Path.Combine(actualDirectory, name);
                    foreach (var ext in IMAGE_EXTENSIONS)
                    {
                        var file = new FileInfo(fileBase + ext);
                        if (file.Exists)
                        {
                            var image = YARGImage.Load(file);
                            if (image != null)
                            {
                                return new BackgroundResult(image);
                            }
                        }
                    }
                }
            }
            return null;
        }

        public override FixedArray<byte>? LoadMiloData()
        {
            var bytes = base.LoadMiloData();
            if (bytes != null)
            {
                return bytes;
            }
            return _miloListing?.LoadAllBytes();
        }

        protected override Stream? GetMidiStream()
        {
            if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiWrite))
                return null;
            return _midiListing.CreateStream();
        }

        protected override FixedArray<byte>? LoadMidiFile(Stream? file)
        {
            if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiWrite))
            {
                return null;
            }
            return _midiListing.LoadAllBytes(file!);
        }

        protected override FixedArray<byte>? LoadRawImageData()
        {
            var bytes = base.LoadRawImageData();
            if (bytes != null)
            {
                return bytes;
            }
            return _imgListing?.LoadAllBytes();
        }

        protected override Stream? GetMoggStream()
        {
            var stream = base.GetMoggStream();
            if (stream != null)
            {
                return stream;
            }
            return _moggListing?.CreateStream();
        }

        protected override bool IsMoggValid(Stream? stream)
        {
            using var mogg = base.GetMoggStream();
            if (mogg != null)
            {
                int version = mogg.Read<int>(Endianness.Little);
                return version == 0x0A || version == 0xf0;
            }
            return _moggListing != null && CONFileListing.GetMoggVersion(_moggListing, stream!) == 0x0A;
        }
    }
}
