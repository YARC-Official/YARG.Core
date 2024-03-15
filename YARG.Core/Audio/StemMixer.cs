using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Audio
{
    public abstract class StemMixer : IDisposable
    {
        private bool _disposed;

        protected readonly AudioManager _manager;
        protected readonly List<StemChannel> _channels = new();

        protected float _speed;
        protected double _length;
        protected bool _isPlaying = false;
        protected Action? _songEnd;

        public double Length => _length;
        public IReadOnlyList<StemChannel> Channels => _channels;
        public bool IsPlaying => _isPlaying && GetPosition() < _length;

        public abstract event Action SongEnd;

        protected StemMixer(AudioManager manager, float speed)
        {
            _manager = manager;
            _speed = speed;

            _manager.AddMixer(this);
        }

        public StemChannel? this[SongStem stem] => _channels.Find(x => x.Stem == stem);

        public abstract int Play(bool restart = false);
        public abstract void FadeIn(float maxVolume);
        public abstract void FadeOut();
        public abstract int Pause();
        public abstract double GetPosition(bool bufferCompensation = true);
        public abstract float GetVolume();
        public abstract void SetPosition(double position, bool bufferCompensation = true);
        public abstract void SetVolume(double volume);
        public abstract int GetData(float[] buffer);
        public abstract void SetSpeed(float speed);
        public abstract bool AddChannel(SongStem stem);
        public abstract bool AddChannel(SongStem stem, Stream stream);
        public abstract bool AddChannel(SongStem stem, int[] indices, float[] panning);
        public abstract bool RemoveChannel(SongStem stemToRemove);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Pause();
                if (disposing)
                {
                    DisposeManagedResources();
                }
                DisposeUnmanagedResources();
                _disposed = true;
            }
        }

        ~StemMixer()
        {
            _manager.RemoveMixer(this);
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }
    }
}
