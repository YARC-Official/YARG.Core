using System;
using System.IO;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Venue;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song
{
    public sealed class UnpackedRBCONEntry : RBCONEntry
    {
        private readonly string _nodename = string.Empty;

        public readonly AbridgedFileInfo_Length? _dta;
        public readonly AbridgedFileInfo_Length? _midi;

        protected override DateTime MidiLastUpdate => _midi!.Value.LastUpdatedTime;
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

        public static UnpackedRBCONEntry? TryLoadFromCache(string directory, in AbridgedFileInfo_Length dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string subname = reader.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = AbridgedFileInfo_Length.TryParseInfo(midiPath, reader);
            if (midiInfo == null)
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
            return new UnpackedRBCONEntry(midiInfo.Value, dta, songDirectory, subname, updateMidi, upgrade, reader, strings);
        }

        public static UnpackedRBCONEntry LoadFromCache_Quick(string directory, in AbridgedFileInfo_Length? dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            string subname = reader.ReadString();
            string songDirectory = Path.Combine(directory, subname);

            string midiPath = Path.Combine(songDirectory, subname + ".mid");
            var midiInfo = new AbridgedFileInfo_Length(midiPath, reader);

            AbridgedFileInfo_Length? updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo_Length(reader) : null;

            var upgrade = upgrades.TryGetValue(nodename, out var node) ? node.Item2 : null;
            return new UnpackedRBCONEntry(midiInfo, dta, songDirectory, subname, updateMidi, upgrade, reader, strings);
        }

        private UnpackedRBCONEntry(AbridgedFileInfo_Length midi, AbridgedFileInfo_Length? dta, string directory, string nodename,
            AbridgedFileInfo_Length? updateMidi, IRBProUpgrade? upgrade, BinaryReader reader, CategoryCacheStrings strings)
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

            _midi = new AbridgedFileInfo_Length(midiInfo);
            _dta = group.DTA;
        }

        public override void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(_nodename);
            var info = _midi!.Value;
            writer.Write(info.LastUpdatedTime.ToBinary());
            writer.Write(info.Length);
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

            var info = new FileInfo(Path.Combine(Directory, "gen", _nodename + ".milo_xbox"));
            if (!info.Exists)
            {
                return null;
            }
            return MemoryMappedArray.Load(info);
        }

        protected override Stream? GetMidiStream()
        {
            if (_dta == null || !_dta.Value.IsStillValid() || !_midi!.Value.IsStillValid())
            {
                return null;
            }
            return new FileStream(_midi.Value.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override FixedArray<byte>? LoadMidiFile(Stream? file)
        {
            if (_dta == null || !_dta.Value.IsStillValid() || !_midi!.Value.IsStillValid())
            {
                return null;
            }
            return MemoryMappedArray.Load(_midi.Value);
        }

        protected override FixedArray<byte>? LoadRawImageData()
        {
            var bytes = base.LoadRawImageData();
            if (bytes != null)
            {
                return bytes;
            }

            var info = new FileInfo(Path.Combine(Directory, "gen", _nodename + "_keep.png_xbox"));
            if (!info.Exists)
            {
                return null;
            }
            return MemoryMappedArray.Load(info);
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
