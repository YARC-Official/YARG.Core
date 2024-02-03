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

        public AudioChannel(SongStem stem, Stream? stream)
        {
            Stem = stem;
            Stream = stream;
        }
    }

    public class MoggChannel : AudioChannel
    {
        public readonly int[]? Indices;
        public readonly float[]? Panning;

        public MoggChannel(SongStem stem, Stream? stream, int[] indices, float[] panning)
            : base(stem, stream)
        {
            Indices = indices;
            Panning = panning;
        }
    }
}
