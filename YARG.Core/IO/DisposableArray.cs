using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public sealed unsafe class DisposableArray<T> : IDisposable
        where T : unmanaged
    {
        public readonly T* Ptr;
        public readonly int Length;

        private readonly Counter counter;
        private bool disposedValue;

        public static DisposableArray<T> Create(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Create(fs);
        }

        public static DisposableArray<T> Create(Stream stream)
        {
            return Create(stream, (int) stream.Length);
        }

        public static DisposableArray<T> Create(Stream stream, int length)
        {
            if (stream.Position + length > stream.Length)
                throw new EndOfStreamException();

            byte* buffer = (byte*) Marshal.AllocHGlobal(length);
            stream.Read(new Span<byte>(buffer, length));
            return new DisposableArray<T>(buffer, length);
        }

        private DisposableArray(byte* ptr, int bytes)
        {
            Ptr = (T*) ptr;
            Length = bytes / sizeof(T);
            counter = new Counter();
        }

        public DisposableArray(int length)
        {
            Length = length;

            int bufferLength = length * sizeof(T);
            Ptr = (T*) Marshal.AllocHGlobal(bufferLength);
            counter = new Counter();
        }

        public DisposableArray<T> Clone()
        {
            return new DisposableArray<T>(this);
        }

        private DisposableArray(DisposableArray<T> other)
        {
            Ptr = other.Ptr;
            Length = other.Length;
            counter = other.counter;
            counter.Increment();
        }

        public ref T this[int index]
        {
            get
            {
                if (0 <= index && index < Length)
                    return ref Ptr[index];
                throw new IndexOutOfRangeException();
            }
        }

        public Span<T> Slice(int offset, int count)
        {
            if (0 <= offset && offset + count <= Length)
                return new Span<T>(Ptr + offset, count);
            throw new IndexOutOfRangeException();
        }

        public IntPtr IntPtr => (IntPtr) Ptr;
        public Span<T> Span => new(Ptr, Length);
        public T[] ToArray()
        {
            var array = new T[Length];
            fixed (T* ptr = array)
                Unsafe.CopyBlock(ptr, Ptr, (uint)(Length * sizeof(T)));
            return array;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                counter.Decrement();
                if (counter.Count == 0)
                    Marshal.FreeHGlobal((IntPtr) Ptr);
                disposedValue = true;
            }
        }

        ~DisposableArray()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
