﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public interface IStemChannel : IDisposable
    {
        public SongStem Stem { get; }
        public double LengthD { get; }
        public float LengthF => (float) LengthD;

        public double Volume { get; }

        public event Action ChannelEnd;

        public int Load(float speed);

        public void FadeIn(float maxVolume);
        public Task FadeOut(CancellationToken token = default);

        public void SetVolume(double newVolume);

        public void SetReverb(bool reverb);

        public void SetSpeed(float speed);
        public void SetWhammyPitch(float percent);

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);

        public double GetLengthInSeconds();
    }
}