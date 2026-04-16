using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    internal abstract class UnpackedConsolePackageEntry : RBCONEntry
    {
        protected abstract YARGImage DXTImageLoader(string path);


        protected DateTime _midiLastWrite;

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

        protected YARGImage? LoadAlbumData(string fileExtension)
        {
            var image = LoadUpdateAlbumData();
            if (image == null)
            {
                string path = Path.Combine(_root.FullName, _subName, "gen", _subName + "_keep" + fileExtension);
                if (File.Exists(path))
                {
                    image = DXTImageLoader(path);
                }
            }
            return image;
        }

        public override BackgroundResult? LoadBackground()
        {
            string yarground = Path.Combine(_root.FullName, _subName, YARGROUND_FULLNAME);
            if (File.Exists(yarground))
            {
                var stream = File.OpenRead(yarground);
                return new BackgroundResult(BackgroundType.Yarground, stream);
            }

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
                        if (image != null)
                        {
                            return new BackgroundResult(image);
                        }
                    }
                }
            }
            return null;
        }

        protected FixedArray<byte>? LoadMiloData(string fileExtension)
        {
            var data = LoadUpdateMiloData();
            if (data == null)
            {
                string path = Path.Combine(_root.FullName, _subName, "gen", _subName + fileExtension);
                if (File.Exists(path))
                {
                    data = FixedArray.LoadFile(path);
                }
            }
            return data;
        }

        protected FixedArray<byte>? GetMainMidiData(string fileExtension)
        {
            string path = Path.Combine(_root.FullName, _subName, _subName + fileExtension);
            return File.Exists(path) ? FixedArray.LoadFile(path) : null;
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

        protected UnpackedConsolePackageEntry(in AbridgedFileInfo root, string nodeName)
            : base(in root, nodeName) {}

    }
}
