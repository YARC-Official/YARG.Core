using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace YARG.Core.IO
{
    public sealed unsafe class NativeArray<T> : FixedArray<T>
        where T : unmanaged
    {
        public static NativeArray<T> Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Load(fs);
        }

        public static NativeArray<T> Load(Stream stream)
        {
            return Load(stream, (int) stream.Length);
        }

        public static NativeArray<T> Load(Stream stream, int length)
        {
            if (stream.Position + length > stream.Length)
                throw new EndOfStreamException();

            byte* buffer = (byte*) Marshal.AllocHGlobal(length);
            stream.Read(new Span<byte>(buffer, length));
            return new NativeArray<T>((T*) buffer, length / sizeof(T));
        }

        public static NativeArray<T> Alloc(int length)
        {
            int bufferLength = length * sizeof(T);
            var ptr = (T*) Marshal.AllocHGlobal(bufferLength);
            return new NativeArray<T>(ptr, length);
        }

        private unsafe NativeArray(T* ptr, int length)
            : base(ptr, length) { }

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
