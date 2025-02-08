using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Venue;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    internal sealed class UnpackedRBCONEntry : RBCONEntry
    {
        private DateTime _midiLastWrite;

        public override EntryType SubType => EntryType.ExCON;
        public override string SortBasedLocation => Path.Combine(_root.FullName, _subName);
        public override string ActualLocation => Path.Combine(_root.FullName, _subName);
        protected override DateTime MidiLastWriteTime => _midiLastWrite;

        internal override void Serialize(MemoryStream stream, CacheWriteIndices node)
        {
            stream.Write(_subName);
            stream.Write(_midiLastWrite.ToBinary(), Endianness.Little);
            base.Serialize(stream, node);
        }

        public override YARGImage LoadAlbumData()
        {
            var image = LoadUpdateAlbumData();
            if (!image.IsAllocated)
            {
                string path = Path.Combine(_root.FullName, _subName, "gen", _subName + "_keep.png_xbox");
                if (File.Exists(path))
                {
                    image = YARGImage.LoadDXT(path);
                }
            }
            return image;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            if ((options & BackgroundType.Yarground) > 0)
            {
                string yarground = Path.Combine(_root.FullName, _subName, YARGROUND_FULLNAME);
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
                    var fileBase = Path.Combine(_root.FullName, _subName, name);
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
                    var fileBase = Path.Combine(_root.FullName, _subName, name);
                    foreach (var ext in IMAGE_EXTENSIONS)
                    {
                        string imageFile = fileBase + ext;
                        if (File.Exists(imageFile))
                        {
                            var image = YARGImage.Load(imageFile);
                            if (image.IsAllocated)
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
            var data = LoadUpdateMiloData();
            if (!data.IsAllocated)
            {
                string path = Path.Combine(_root.FullName, _subName, "gen", _subName + ".mogg");
                if (File.Exists(path))
                {
                    data = FixedArray.LoadFile(path);
                }
            }
            return data;
        }

        protected override FixedArray<byte> GetMainMidiData()
        {
            string path = Path.Combine(_root.FullName, _subName, _subName + ".mid");
            return File.Exists(path) ? FixedArray.LoadFile(path) : FixedArray<byte>.Null;
        }

        protected override Stream? GetMoggStream()
        {
            var stream = LoadUpdateMoggStream();
            if (stream == null)
            {
                string path = Path.Combine(_root.FullName, _subName, _subName + ".mogg");
                if (File.Exists(path))
                {
                    stream = File.OpenRead(path);
                }
            }
            return stream;
        }

        private UnpackedRBCONEntry(in AbridgedFileInfo root, string nodeName)
            : base(in root, nodeName) {}

        public static ScanExpected<RBCONEntry> Create(in RBScanParameters parameters)
        {
            try
            {
                var entry = new UnpackedRBCONEntry(in parameters.Root, parameters.NodeName)
                {
                    _updateDirectoryAndDtaLastWrite = parameters.UpdateDirectory,
                    _updateMidiLastWrite = parameters.UpdateMidi,
                    _upgrade = parameters.Upgrade
                };
                entry._metadata.Playlist = parameters.DefaultPlaylist;

                var location = ProcessDTAs(entry, parameters.BaseDta, parameters.UpdateDta, parameters.UpgradeDta);
                if (!location)
                {
                    return new ScanUnexpected(location.Error);
                }

                entry._subName = location.Value[6..location.Value.IndexOf('/', 6)];

                string songDirectory = Path.Combine(parameters.Root.FullName, entry._subName);
                var midiInfo = new FileInfo(Path.Combine(songDirectory, entry._subName + ".mid"));
                if (!midiInfo.Exists)
                {
                    return new ScanUnexpected(ScanResult.MissingCONMidi);
                }

                string moggPath = Path.Combine(songDirectory, entry._subName + ".mogg");
                if (File.Exists(moggPath))
                {
                    using var moggStream = new FileStream(moggPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                    if (moggStream.Read<int>(Endianness.Little) != UNENCRYPTED_MOGG)
                    {
                        return new ScanUnexpected(ScanResult.MoggError);
                    }
                }
                else
                {
                    return new ScanUnexpected(ScanResult.MoggError);
                }

                using var mainMidi = FixedArray.LoadFile(midiInfo.FullName);
                var result = ScanMidis(entry, in mainMidi);
                if (result != ScanResult.Success)
                {
                    return new ScanUnexpected(result);
                }
                entry._midiLastWrite = AbridgedFileInfo.NormalizedLastWrite(midiInfo);
                entry.SetSortStrings();
                return entry;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                return new ScanUnexpected(ScanResult.DTAError);
            }
        }

        public static UnpackedRBCONEntry? TryDeserialize(in AbridgedFileInfo root, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string subname = stream.ReadString();
            string midiPath = Path.Combine(root.FullName, subname, subname + ".mid");
            var midiInfo = new FileInfo(midiPath);
            if (!midiInfo.Exists)
            {
                return null;
            }

            var midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            if (midiLastWrite != midiInfo.LastWriteTime)
            {
                return null;
            }

            var entry = new UnpackedRBCONEntry(in root, nodeName)
            {
                _subName = subname,
                _midiLastWrite = midiLastWrite,
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static UnpackedRBCONEntry ForceDeserialize(in AbridgedFileInfo root, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            var entry = new UnpackedRBCONEntry(in root, nodeName)
            {
                _subName = stream.ReadString(),
                _midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little)),
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }
    }
}
