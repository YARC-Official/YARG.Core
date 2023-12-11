using System;

namespace YARG.Core.Audio
{
    public interface ISfxSample<TSfxSample> : IDisposable
        where TSfxSample : Enum
    {
        public TSfxSample Sample { get; }

        public float Volume { get; set; }

        public void Play();
    }
}