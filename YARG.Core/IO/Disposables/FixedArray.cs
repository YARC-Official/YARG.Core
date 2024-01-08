using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public sealed unsafe class FixedArray<T> : RefCounter<FixedArray<T>>, IEnumerable<T>
        where T : unmanaged
    {
        public readonly T* Ptr;
        public readonly int Length;

        private bool _disposedValue;
        private readonly Action _disposal;

        public static FixedArray<T> Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Load(fs);
        }

        public static FixedArray<T> Load(Stream stream)
        {
            int length = (int)(stream.Length - stream.Position);
            return Load(stream, length);
        }

        public static FixedArray<T> Load(Stream stream, int length)
        {
            if (stream.Position + length > stream.Length)
                throw new EndOfStreamException();

            byte* buffer = (byte*)Marshal.AllocHGlobal(length);
            stream.Read(new Span<byte>(buffer, length));
            return new FixedArray<T>((T*)buffer, length / sizeof(T), () => Marshal.FreeHGlobal((IntPtr)buffer));
        }

        public static FixedArray<T> Alloc(int length)
        {
            int bufferLength = length * sizeof(T);
            var ptr = (T*)Marshal.AllocHGlobal(bufferLength);
            return new FixedArray<T>(ptr, length, () => Marshal.FreeHGlobal((IntPtr)ptr));
        }

        public static FixedArray<T> Pin(T[] array)
        {
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            return new FixedArray<T>((T*)handle.AddrOfPinnedObject(), array.Length, () => handle.Free());
        }

        public FixedArray(T* ptr, int length, Action disposal)
        {
            Ptr = ptr;
            Length = length;
            _disposal = disposal;
        }

        public ref T this[int index]
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (index < 0 || Length <= index )
                {
                    throw new IndexOutOfRangeException();
                }
                return ref Ptr[index];
            }
        }

        public Span<T> Slice(int offset, int count)
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (0 <= offset && offset + count <= Length)
                return new Span<T>(Ptr + offset, count);
            throw new IndexOutOfRangeException();
        }

        public IntPtr IntPtr
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return (IntPtr)Ptr;
            }
        }

        public Span<T> Span
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return new Span<T>(Ptr, Length);
            }
        }

        public ReadOnlySpan<T> ReadOnlySpan
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return new ReadOnlySpan<T>(Ptr, Length);
            }
        }

        public T[] ToArray()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            var array = new T[Length];
            Span.CopyTo(array);
            return array;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposal();
                _disposedValue = true;
            }
        }

        public IEnumerator GetEnumerator() { return new Enumerator(this); }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly FixedArray<T> _arr;
            private int _index;

            internal Enumerator(FixedArray<T> arr)
            {
                _arr = arr.AddRef();
                _index = -1;
            }

            public void Dispose()
            {
                _arr.Dispose();
            }

            public bool MoveNext()
            {
                ++_index;
                return _index < _arr.Length;
            }

            public readonly T Current => _arr[_index];

            readonly object IEnumerator.Current => Current;

            void IEnumerator.Reset()
            {
                _index = -1;
            }
        }
    }
}
