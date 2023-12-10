using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface IStemMixer : IDisposable
    {
        public int StemsLoaded { get; }

        public bool IsPlaying { get; }

        public event Action SongEnd;

        public IReadOnlyDictionary<SongStem, List<IStemChannel>> Channels { get; }

        public IStemChannel LeadChannel { get; }

        public float Volume { get; set; }
        public float Speed { get; set; }

        public bool Create();

        public int Play(bool restart = false);

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public int Pause();

        public double GetPosition(bool desyncCompensation = true);

        public void SetPosition(double position, bool desyncCompensation = true);

        public int AddChannel(IStemChannel channel);

        public bool RemoveChannel(IStemChannel channel);

        public IStemChannel[] GetChannels(SongStem stem);
    }
}