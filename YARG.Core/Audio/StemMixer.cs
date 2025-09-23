using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Audio
{
    public abstract class StemMixer : IDisposable
    {
        public struct StemInfo
        {
            public SongStem Stem;
            public int[]    Indices;
            public float[]  Panning;

            public StemInfo(SongStem stem, int[] indices = null, float[] panning = null)
            {
                Stem = stem;
                Indices = indices;
                Panning = panning;
            }

            public void Deconstruct(out SongStem stem, out int[] indices, out float[] panning)
            {
                stem = Stem;
                indices = Indices;
                panning = Panning;
            }
        }

        private bool _disposed;
        private bool _isPaused = true;

        protected readonly AudioManager _manager;
        protected readonly List<StemChannel> _channels = new();
        protected readonly bool _clampStemVolume;

        protected double _length;
        protected Action? _songEnd;

        public readonly string Name;

        public double Length => _length;
        public IReadOnlyList<StemChannel> Channels => _channels;
        public bool IsPaused => _isPaused;

        public abstract event Action SongEnd;

        protected StemMixer(string name, AudioManager manager,bool clampStemVolume)
        {
            Name = name;
            _manager = manager;
            _clampStemVolume = clampStemVolume;

            _manager.AddMixer(this);
        }

        public StemChannel? this[SongStem stem] => _channels.Find(x => x.Stem == stem);

        public int Play(bool restartBuffer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }

                int ret = Play_Internal(restartBuffer);
                if (ret != 0)
                {
                    return ret;
                }
                _isPaused = false;
                return 0;
            }
        }

        public void FadeIn(double maxVolume, double duration)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeIn_Internal(maxVolume, duration);
                }
            }
        }
        public void FadeOut(double duration)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeOut_Internal(duration);
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

                int ret = Pause_Internal();
                if (ret != 0)
                {
                    return ret;
                }
                _isPaused = true;
                return 0;
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


        /// <summary>
        /// Get FFT Data
        /// <paramref name="fftSize"/> is log2 of the number of samples to process
        /// eg with <paramref name="fftSize"/> equal to 9, 512 samples will be processed
        /// The number of bytes read from the channel (to perform the FFT) is returned
        /// Data is returned as a float in [0; 1] range
        /// The 1st bin contains the DC component, the 2nd contains the amplitude at 1/2048 of the channel's sample rate, followed by the amplitude at 2/2048, 3/2048, etc.
        /// with complex == false only real part of FFT is returned
        /// and (1 << fftSize) / 2 values are filled (the magnitudes of the first half of an FFT result are returned)
        /// with complex == true
        /// Return the complex FFT result rather than the magnitudes. This increases the amount of data returned (as listed above) fourfold, as it returns real and imaginary parts and the full FFT result (not only the first half). The real and imaginary parts are interleaved in the returned data.
        /// </summary>
        public int GetFFTData(float[] buffer, int fftSize, bool complex)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return GetFFTData_Internal(buffer, fftSize, complex);
            }
        }

        /// Get sample data
        /// returned floats are in [-1; 1] range
        /// returns value of bytes read from a channel
        public int GetSampleData(float[] buffer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return GetSampleData_Internal(buffer);
            }
        }

        public int GetLevel(float[] level)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return GetLevel_Internal(level);
            }
        }

        public void SetSpeed(float speed, bool shiftPitch)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetSpeed_Internal(speed, shiftPitch);
                }
            }
        }

        public bool AddChannel(Stream stream, SongStem songStem)
        {
            return AddChannels(stream, new StemInfo(songStem));
        }

        public bool AddChannels(Stream stream, params StemInfo[] stemInfos)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannels_Internal(stream, stemInfos);
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

        internal void ToggleBuffer(bool enable)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    ToggleBuffer_Internal(enable);
                }
            }
        }

        internal void SetBufferLength(int length)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetBufferLength_Internal(length);
                }
            }
        }

        protected abstract int Play_Internal(bool restartBuffer);
        protected abstract void FadeIn_Internal(double maxVolume, double duration);
        protected abstract void FadeOut_Internal(double duration);
        protected abstract int Pause_Internal();
        protected abstract double GetPosition_Internal();
        protected abstract double GetVolume_Internal();
        protected abstract void SetPosition_Internal(double position);
        protected abstract void SetVolume_Internal(double volume);
        protected abstract int  GetSampleData_Internal(float[] buffer);
        protected abstract int  GetFFTData_Internal(float[] buffer, int fftSize, bool complex);
        protected abstract int GetLevel_Internal(float[] level);
        protected abstract void SetSpeed_Internal(float speed, bool shiftPitch);
        protected abstract bool AddChannels_Internal(Stream stream, params StemInfo[] stemInfos);
        protected abstract bool RemoveChannel_Internal(SongStem stemToRemove);
        protected abstract void ToggleBuffer_Internal(bool enable);
        protected abstract void SetBufferLength_Internal(int length);

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