using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public sealed class PtrCounter
    {
        private int _count = 1;
        public int Count => _count;

        public void Increment() => ++_count;
        public void Decrement() => --_count;
    }

    public sealed unsafe class DisposableArray<T> : IDisposable
        where T : unmanaged
    {
        public readonly T* Ptr;
        public readonly int Size;

        private readonly PtrCounter counter;
        private bool disposedValue;

        public static DisposableArray<T> Create(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Create(fs);
        }

        public static DisposableArray<T> Create(Stream stream)
        {
            int fileLength = (int) stream.Length;
            byte* buffer = (byte*) Marshal.AllocHGlobal(fileLength);
            stream.Read(new Span<byte>(buffer, fileLength));

            return new DisposableArray<T>(buffer, fileLength);
        }

        private DisposableArray(byte* ptr, int bytes)
        {
            Ptr = (T*) ptr;
            Size = bytes / sizeof(T);
            counter = new PtrCounter();
        }

        public DisposableArray(int length)
        {
            Size = length;

            int bufferLength = length * sizeof(T);
            Ptr = (T*) Marshal.AllocHGlobal(bufferLength);
            counter = new PtrCounter();
        }

        public DisposableArray(DisposableArray<T> other)
        {
            Ptr = other.Ptr;
            Size = other.Size;
            counter = other.counter;
            counter.Increment();
        }

        public ref T this[int index]
        {
            get
            {
                if (0 <= index && index < Size)
                    return ref Ptr[index];
                throw new IndexOutOfRangeException();
            }
        }

        public Span<T> Slice(int offset, int count)
        {
            if (0 <= offset && offset + count <= Size)
                return new Span<T>(Ptr + offset, count);
            throw new IndexOutOfRangeException();
        }

        public IntPtr IntPtr => (IntPtr) Ptr;
        public Span<T> Span => new(Ptr, Size);
        public T[] ToArray()
        {
            var array = new T[Size];
            fixed (T* ptr = array)
                Unsafe.CopyBlock(ptr, Ptr, (uint)(Size * sizeof(T)));
            return array;
        }

        public UnmanagedMemoryStream ToUnmanagedStream() => new((byte*)Ptr, Size);

        public static implicit operator ReadOnlySpan<T>(DisposableArray<T> arr) => arr.Span;

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
