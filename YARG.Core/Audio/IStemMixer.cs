using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface IStemMixer : IDisposable
    {
        public event Action SongEnd;

        public int StemsLoaded { get; }

        public IReadOnlyDictionary<SongStem, List<IStemChannel>> Channels { get; }

        public IStemChannel LeadChannel { get; }

        public bool IsPlaying { get; }

        public float Volume { get; set; }
        public float Speed { get; set; }

        public bool Create();

        public int AddChannel(IStemChannel channel);
        public bool RemoveChannel(IStemChannel channel);

        public void Play(bool restart = false);
        public void Pause();

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public IStemChannel[] GetChannels(SongStem stem);
    }
}