using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace YARG.Core.Audio
{
    public abstract class StemMixer : IDisposable
    {
        private bool _disposed;
        
        protected readonly AudioManager _manager;
        protected readonly List<StemChannel> _channels = new();
        protected readonly bool _clampStemVolume;

        protected float _speed;
        protected double _length;
        protected bool _isPlaying = false;
        protected Action? _songEnd;

        public readonly string Name;

        public double Length => _length;
        public IReadOnlyList<StemChannel> Channels => _channels;
        public bool IsPlaying => _isPlaying && GetPosition() < _length;

        public abstract event Action SongEnd;

        protected StemMixer(string name, AudioManager manager, float speed, bool clampStemVolume)
        {
            Name = name;
            _manager = manager;
            _speed = speed;
            _clampStemVolume = clampStemVolume;

            _manager.AddMixer(this);
        }

        public StemChannel? this[SongStem stem] => _channels.Find(x => x.Stem == stem);

        public int Play(bool restart = false)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return Play_Internal(restart);
            }
        }

        public void FadeIn(float maxVolume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeIn_Internal(maxVolume);
                }
            }
        }
        public void FadeOut()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeOut_Internal();
                }
            }
        }

        public int Pause()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return Pause_Internal();
            }
        }

        public double GetPosition()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return 0;
                }
                return GetPosition_Internal();
            }
        }

        public double GetVolume()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return 0;
                }
                return GetVolume_Internal();
            }
        }
        public void SetPosition(double position)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetPosition_Internal(position);
                }
            }
        }

        public void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(volume);
                }
            }
        }

        public int GetData(float[] buffer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return GetData_Internal(buffer);
            }
        }

        public void SetSpeed(float speed)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetSpeed_Internal(speed);
                }
            }
        }

        public bool AddChannel(SongStem stem)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem);
            }
        }

        public bool AddChannel(SongStem stem, Stream stream)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem, stream);
            }
        }

        public bool AddChannel(SongStem stem, int[] indices, float[] panning)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem, indices, panning);
            }
        }

        public bool RemoveChannel(SongStem stemToRemove)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return RemoveChannel_Internal(stemToRemove);
            }
        }

        protected abstract int Play_Internal(bool restart);
        protected abstract void FadeIn_Internal(float maxVolume);
        protected abstract void FadeOut_Internal();
        protected abstract int Pause_Internal();
        protected abstract double GetPosition_Internal();
        protected abstract double GetVolume_Internal();
        protected abstract void SetPosition_Internal(double position);
        protected abstract void SetVolume_Internal(double volume);
        protected abstract int  GetData_Internal(float[] buffer);
        protected abstract void SetSpeed_Internal(float speed);
        protected abstract bool AddChannel_Internal(SongStem stem);
        protected abstract bool AddChannel_Internal(SongStem stem, Stream stream);
        protected abstract bool AddChannel_Internal(SongStem stem, int[] indices, float[] panning);
        protected abstract bool RemoveChannel_Internal(SongStem stemToRemove);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Pause();
                    _songEnd = null;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _manager.RemoveMixer(this);
                    _disposed = true;
                }
            }
        }

        ~StemMixer()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }
    }
}
