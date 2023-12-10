using System;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface ISongContext : IDisposable
    {
        public event Action SongEnd;

        public double Length { get; }

        public bool IsPlaying { get; }

        public float Volume { get; set; }
        public float Speed { get; set; }

        public void Play();
        public void Pause();

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);

        public float GetVolume(SongStem stem);
        public void SetVolume(SongStem stem, float volume);

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public void SetPitchBend(SongStem stem, float semitones);
        public void SetReverb(SongStem stem, bool enabled);
    }
}