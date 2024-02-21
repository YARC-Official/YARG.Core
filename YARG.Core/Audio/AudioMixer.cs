using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Audio
{
    public class AudioMixer
    {
        public readonly Stream? Stream;
        public readonly List<AudioChannel> Channels = new();

        public AudioMixer(Stream? stream = null)
        {
            Stream = stream;
        }
    }
}
