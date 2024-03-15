using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class BackgroundResult
    {
        public readonly BackgroundType Type;
        public readonly Stream? Stream;

        public BackgroundResult(BackgroundType type, Stream? stream)
        {
            Type = type;
            Stream = stream;
        }
    }

    public abstract partial class SongEntry
    {
        public abstract SongChart? LoadChart();
        public abstract StemMixer? LoadAudio(AudioManager manager, float speed, params SongStem[] ignoreStems);
        public abstract StemMixer? LoadPreviewAudio(AudioManager manager, float speed);
        public abstract byte[]? LoadAlbumData();
        public abstract BackgroundResult? LoadBackground(BackgroundType options);
        public abstract byte[]? LoadMiloData();
    }
}
