using System;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class BackgroundResult : IDisposable
    {
        private YARGImage?     _image;
        public  BackgroundType Type   { get; }
        public  Stream?        Stream { get; }

        public YARGImage Image => _image;

        public BackgroundResult(BackgroundType type, Stream stream)
        {
            _image = null;
            Type = type;
            Stream = stream;
        }

        public BackgroundResult(YARGImage image)
        {
            _image = image;
            Type = BackgroundType.Image;
            Stream = null;
        }

        public void Dispose()
        {
            _image?.Dispose();
            Stream?.Dispose();
        }
    }

    public abstract partial class SongEntry
    {
        public abstract SongChart? LoadChart();
        public abstract StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems);
        public abstract StemMixer? LoadPreviewAudio(float speed);
        public abstract YARGImage? LoadAlbumData();
        public abstract BackgroundResult? LoadBackground();
        public abstract FixedArray<byte>? LoadMiloData();
    }
}
