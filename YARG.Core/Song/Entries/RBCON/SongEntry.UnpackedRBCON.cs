using System;
using System.IO;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public sealed class UnpackedRBCONEntry : RBCONEntry
    {
        private readonly string _nodename = string.Empty;

        public readonly AbridgedFileInfo? _dta;
        public readonly AbridgedFileInfo? _midi;

        protected override DateTime MidiLastUpdate => _midi!.LastUpdatedTime;
        public override string Directory { get; } = string.Empty;
        public override EntryType SubType => EntryType.ExCON;

        public static (ScanResult, UnpackedRBCONEntry?) ProcessNewEntry(UnpackedCONGroup group, string nodename, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                var song = new UnpackedRBCONEntry(group, nodename, reader, updates, upgrades);
                if (song._midi == null)
                {
                    return (ScanResult.MissingMidi, null);
                }

                var result = song.ParseRBCONMidi(null);
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

        public static UnpackedRBCONEntry? TryLoadFromCache(string directory, AbridgedFileInfo dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
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

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Item2 : null;
            return new UnpackedRBCONEntry(midiInfo, dta, songDirectory, subname, updateMidi, upgrade, reader, strings);
        }

        public static UnpackedRBCONEntry LoadFromCache_Quick(string directory, AbridgedFileInfo? dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string subname = reader.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = new AbridgedFileInfo(midiPath, reader);

            var updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Item2 : null;
            return new UnpackedRBCONEntry(midiInfo, dta, songDirectory, subname, updateMidi, upgrade, reader, strings);
        }

        private UnpackedRBCONEntry(AbridgedFileInfo midi, AbridgedFileInfo? dta, string directory, string nodename,
            AbridgedFileInfo? updateMidi, IRBProUpgrade? upgrade, BinaryReader reader, CategoryCacheStrings strings)
            : base(updateMidi, upgrade, reader, strings)
        {
            Directory = directory;

            _midi = midi;
            _dta = dta;
            _nodename = nodename;
        }

        private UnpackedRBCONEntry(UnpackedCONGroup group, string nodename, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
            : base()
        {
            var results = Init(nodename, reader, updates, upgrades, group.DefaultPlaylist);
            if (!results.location.StartsWith($"songs/" + nodename))
                nodename = results.location.Split('/')[1];
            _nodename = nodename;

            Directory = Path.Combine(group.Location, nodename);
            string midiPath = Path.Combine(Directory, nodename + ".mid");

            FileInfo midiInfo = new(midiPath);
            if (!midiInfo.Exists)
            {
                return;
            }

            _midi = new AbridgedFileInfo(midiInfo);
            _dta = group.DTA;
        }

        public override void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(_nodename);
            writer.Write(_midi!.LastUpdatedTime.ToBinary());
            base.Serialize(writer, node);
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            if ((options & BackgroundType.Yarground) > 0)
            {
                string yarground = Path.Combine(Directory, YARGROUND_FULLNAME);
                if (File.Exists(yarground))
                {
                    var stream = File.OpenRead(yarground);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                foreach (var name in BACKGROUND_FILENAMES)
                {
                    var fileBase = Path.Combine(Directory, name);
                    foreach (var ext in VIDEO_EXTENSIONS)
                    {
                        string videoFile = fileBase + ext;
                        if (File.Exists(videoFile))
                        {
                            var stream = File.OpenRead(videoFile);
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                //                                     No "video"
                foreach (var name in BACKGROUND_FILENAMES[..2])
                {
                    var fileBase = Path.Combine(Directory, name);
                    foreach (var ext in IMAGE_EXTENSIONS)
                    {
                        string imageFile = fileBase + ext;
                        if (File.Exists(imageFile))
                        {
                            var image = YARGImage.Load(imageFile);
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

        public override byte[]? LoadMiloData()
        {
            var bytes = base.LoadMiloData();
            if (bytes != null)
            {
                return bytes;
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
            if (_dta == null || !_dta.IsStillValid() || !_midi!.IsStillValid())
            {
                return null;
            }
            return new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override byte[]? LoadMidiFile(Stream? file)
        {
            if (_dta == null || !_dta.IsStillValid() || !_midi!.IsStillValid())
            {
                return null;
            }
            return File.ReadAllBytes(_midi.FullName);
        }

        protected override byte[]? LoadRawImageData()
        {
            var bytes = base.LoadRawImageData();
            if (bytes != null)
            {
                return bytes;
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
            var stream = base.GetMoggStream();
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

        protected override bool IsMoggValid(Stream? file)
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
