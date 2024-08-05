using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO.Disposables
{
    public sealed unsafe class AllocatedArray<T> : FixedArray<T>
        where T : unmanaged
    {
        public static AllocatedArray<T> Alloc(long length)
        {
            var ptr = (T*)Marshal.AllocHGlobal((int)(length * sizeof(T)));
            return new AllocatedArray<T>(ptr, length);
        }

        public static AllocatedArray<T> ReAlloc(AllocatedArray<T> original, long length)
        {
            GC.SuppressFinalize(original);
            var ptr = (T*) Marshal.ReAllocHGlobal(original.IntPtr, (IntPtr) (length * sizeof(T)));
            return new AllocatedArray<T>(ptr, length);
        }

        public static AllocatedArray<T> Read(Stream stream, long length)
        {
            var buffer = Alloc(length);
            stream.Read(new Span<byte>(buffer.Ptr, (int)(length * sizeof(T))));
            return buffer;
        }

        private AllocatedArray(T* ptr, long length)
            : base(ptr, length) { }

        public Span<T> Span => new(Ptr, (int)Length);

        public Span<T> Slice(long offset, long count)
        {
            if (offset < 0 || Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }
            return new Span<T>(Ptr + offset, (int) count);
        }

        public override void Dispose()
        {
            Marshal.FreeHGlobal(IntPtr);
            GC.SuppressFinalize(this);
        }

        ~AllocatedArray()
        {
            YargLogger.LogWarning("Dev warning: only use AllocatedArray IF YOU MANUALLY DISPOSE! Not doing so defeats the purpose!");
        }
    }
}
