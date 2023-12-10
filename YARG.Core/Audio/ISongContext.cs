using System;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface ISongContext : IDisposable
    {
        public event Action SongEnd;

        public double AudioLength { get; }

        public bool IsPlaying { get; }

        public void Play();
        public void Pause();

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);

        public void SetVolume(double volume);
        public void SetVolume(SongStem stem, double volume);

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public void SetSpeed(float speed);
        public void SetPitchBend(SongStem stem, float semitones);
        public void SetReverb(SongStem stem, bool enabled);
    }
}