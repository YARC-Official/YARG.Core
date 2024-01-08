using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace YARG.Core.IO
{
    public sealed unsafe class PinnedArray<T> : FixedArray<T>
        where T : unmanaged
    {
        private readonly GCHandle _handle;

        public static PinnedArray<T> Pin(T[] array)
        {
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            return new PinnedArray<T>(handle, array.Length);
        }

        private unsafe PinnedArray(GCHandle handle, int length)
            : base((T*)handle.AddrOfPinnedObject(), length)
        {
            _handle = handle;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _handle.Free();
                _disposedValue = true;
            }
        }
    }
}
