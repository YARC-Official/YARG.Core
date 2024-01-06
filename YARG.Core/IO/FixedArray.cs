using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public sealed unsafe class FixedArray<T> : RefCounter<FixedArray<T>>
        where T : unmanaged
    {
        public readonly T* Ptr;
        public readonly int Length;
        private bool _disposedValue;

        public static FixedArray<T> Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Load(fs);
        }

        public static FixedArray<T> Load(Stream stream)
        {
            return Load(stream, (int)stream.Length);
        }

        public static FixedArray<T> Load(Stream stream, int length)
        {
            if (stream.Position + length > stream.Length)
                throw new EndOfStreamException();

            byte* buffer = (byte*)Marshal.AllocHGlobal(length);
            stream.Read(new Span<byte>(buffer, length));
            return new FixedArray<T>((T*)buffer, length / sizeof(T));
        }

        public static FixedArray<T> Alloc(int length)
        {
            int bufferLength = length * sizeof(T);
            var ptr = (T*)Marshal.AllocHGlobal(bufferLength);
            return new FixedArray<T>(ptr, length);
        }

        private FixedArray(T* ptr, int length)
        {
            Ptr = ptr;
            Length = length;
        }

        public ref T this[int index]
        {
            get
            {
                if (_disposedValue)
                    throw new ObjectDisposedException(GetType().Name);

                if (0 <= index && index < Length)
                {
                    return ref Ptr[index];
                }
                throw new IndexOutOfRangeException();
            }
        }

        public Span<T> Slice(int offset, int count)
        {
            if (_disposedValue)
                throw new ObjectDisposedException(GetType().Name);

            if (0 <= offset && offset + count <= Length)
                return new Span<T>(Ptr + offset, count);
            throw new IndexOutOfRangeException();
        }

        public IntPtr IntPtr
        {
            get
            {
                if (_disposedValue)
                    throw new ObjectDisposedException(GetType().Name);
                return (IntPtr)Ptr;
            }
        }

        public Span<T> Span
        {
            get
            {
                if (_disposedValue)
                    throw new ObjectDisposedException(GetType().Name);
                return new Span<T>(Ptr, Length);
            }
        }

        public ReadOnlySpan<T> ReadOnlySpan
        {
            get
            {
                if (_disposedValue)
                    throw new ObjectDisposedException(GetType().Name);
                return new ReadOnlySpan<T>(Ptr, Length);
            }
        }

        public T[] ToArray()
        {
            if (_disposedValue)
                throw new ObjectDisposedException(GetType().Name);

            var array = new T[Length];
            Span.CopyTo(array);
            return array;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Marshal.FreeHGlobal((IntPtr) Ptr);
                _disposedValue = true;
            }
        }
    }
}
