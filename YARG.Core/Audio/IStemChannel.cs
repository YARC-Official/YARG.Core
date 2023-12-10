using System;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface IStemChannel : IDisposable
    {
        public event Action ChannelEnd;

        public SongStem Stem { get; }

        public double Length { get; }

        public float Volume { get; set; }
        public float Speed { get; set; }

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public void SetWhammyPitch(float percent);
        public void SetReverb(bool reverb);

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);
    }
}