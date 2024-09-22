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

        protected override DateTime MidiLastUpdate => _midi!.Value.LastUpdatedTime;
        public override string Location { get; }
        public override string DirectoryActual => Location;
        public override EntryType SubType => EntryType.ExCON;

        public static (ScanResult, UnpackedRBCONEntry?) ProcessNewEntry(UnpackedCONGroup group, string nodename, in YARGTextContainer<byte> container, Dictionary<string, SortedList<DateTime, SongUpdate>> updates, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades)
        {
            try
            {
                var song = new UnpackedRBCONEntry(group, nodename, in container, updates, upgrades);
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

        public static UnpackedRBCONEntry? TryLoadFromCache(string directory, in AbridgedFileInfo dta, string nodename, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade Upgrade)> upgrades, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            string subname = stream.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = AbridgedFileInfo.TryParseInfo(midiPath, stream);
            if (midiInfo == null)
            {
                return null;
            }

            AbridgedFileInfo? updateMidi = null;
            if (stream.ReadBoolean())
            {
                updateMidi = AbridgedFileInfo.TryParseInfo(stream, false);
                if (updateMidi == null)
                {
                    return null;
                }
            }

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Upgrade : null;
            return new UnpackedRBCONEntry(midiInfo.Value, dta, songDirectory, subname, updateMidi, upgrade, stream, strings);
        }

        public static UnpackedRBCONEntry LoadFromCache_Quick(string directory, in AbridgedFileInfo? dta, string nodename, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade Upgrade)> upgrades, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            string subname = stream.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = new AbridgedFileInfo(midiPath, stream);

            AbridgedFileInfo? updateMidi = stream.ReadBoolean() ? new AbridgedFileInfo(stream) : null;

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Upgrade : null;
            return new UnpackedRBCONEntry(midiInfo, dta, songDirectory, subname, updateMidi, upgrade, stream, strings);
        }

        private UnpackedRBCONEntry(AbridgedFileInfo midi, AbridgedFileInfo? dta, string directory, string nodename,
            AbridgedFileInfo? updateMidi, RBProUpgrade? upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(updateMidi, upgrade, stream, strings)
        {
            Location = directory;

            _midi = midi;
            _dta = dta;
            _nodename = nodename;
        }

        private UnpackedRBCONEntry(UnpackedCONGroup group, string nodename, in YARGTextContainer<byte> container, Dictionary<string, SortedList<DateTime, SongUpdate>> updates, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades)
            : base()
        {
            var results = Init(nodename, in container, updates, upgrades, group.DefaultPlaylist);
            if (!results.location.StartsWith($"songs/" + nodename))
                nodename = results.location.Split('/')[1];
            _nodename = nodename;

            Location = Path.Combine(group.Location, nodename);
            string midiPath = Path.Combine(Location, nodename + ".mid");

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
            var info = _midi!.Value;
            writer.Write(info.LastUpdatedTime.ToBinary());
            base.Serialize(writer, node);
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            if ((options & BackgroundType.Yarground) > 0)
            {
                string yarground = Path.Combine(Location, YARGROUND_FULLNAME);
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
                    var fileBase = Path.Combine(Location, name);
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
                    var fileBase = Path.Combine(Location, name);
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

        public override FixedArray<byte> LoadMiloData()
        {
            var bytes = base.LoadMiloData();
            if (bytes.IsAllocated)
            {
                return bytes;
            }

            string filename = Path.Combine(Location, "gen", _nodename + ".milo_xbox");
            return File.Exists(filename) ? FixedArray<byte>.Load(filename) : FixedArray<byte>.Null;
        }

        protected override Stream? GetMidiStream()
        {
            if (_dta == null || !_dta.Value.IsStillValid() || !_midi!.Value.IsStillValid())
            {
                return null;
            }
            return new FileStream(_midi.Value.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override FixedArray<byte> LoadMidiFile(Stream? file)
        {
            return _dta != null && _dta.Value.IsStillValid() && _midi!.Value.IsStillValid()
                ? FixedArray<byte>.Load(_midi.Value.FullName)
                : FixedArray<byte>.Null;
        }

        protected override FixedArray<byte> LoadRawImageData()
        {
            var bytes = base.LoadRawImageData();
            if (bytes.IsAllocated)
            {
                return bytes;
            }

            string filename = Path.Combine(Location, "gen", _nodename + "_keep.png_xbox");
            return File.Exists(filename) ? FixedArray<byte>.Load(filename) : FixedArray<byte>.Null;
        }

        protected override Stream? GetMoggStream()
        {
            var stream = base.GetMoggStream();
            if (stream != null)
            {
                return stream;
            }

            string path = Path.Combine(Location, _nodename + ".yarg_mogg");
            if (File.Exists(path))
            {
                return new YargMoggReadStream(path);
            }

            path = Path.Combine(Location, _nodename + ".mogg");
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
