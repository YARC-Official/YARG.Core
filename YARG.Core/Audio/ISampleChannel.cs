using System;

namespace YARG.Core.Audio
{
    public interface ISampleChannel : IDisposable
    {
        public SfxSample Sample { get; }

        public int Load();

        public void Play();
    }
}