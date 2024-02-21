using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Audio
{
    public class AudioChannel
    {
        public readonly SongStem Stem;
        public readonly Stream? Stream;
        public readonly int[]? Indices;
        public readonly float[]? Panning;

        public AudioChannel(SongStem stem, Stream stream)
        {
            Stem = stem;
            Stream = stream;
        }

        public AudioChannel(SongStem stem, int[] indices, float[] panning)
        {
            Stem = stem;
            Indices = indices;
            Panning = panning;
        }
    }
}
