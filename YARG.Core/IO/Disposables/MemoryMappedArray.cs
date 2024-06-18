using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO.Disposables
{
    public sealed unsafe class MemoryMappedArray : FixedArray<byte>
    {
        public static MemoryMappedArray Load(FileInfo info)
        {
            return Load(info.FullName, info.Length);
        }

        public static MemoryMappedArray Load(in AbridgedFileInfo_Length info)
        {
            return Load(info.FullName, info.Length);
        }

        public static MemoryMappedArray Load(string filename, long length)
        {
            var file = MemoryMappedFile.CreateFromFile(filename, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var accessor = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            return new MemoryMappedArray(file, accessor, ptr, length);
        }

        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _accessor;

        private MemoryMappedArray(MemoryMappedFile file, MemoryMappedViewAccessor accessor, byte* ptr, long length)
            : base(ptr, length)
        {
            _file = file;
            _accessor = accessor;
        }

        public override void Dispose()
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _file.Dispose();
            GC.SuppressFinalize(this);
        }

        ~MemoryMappedArray()
        {
            YargLogger.LogWarning("Dev warning: only use MemoryMappedArray IF YOU MANUALLY DISPOSE! Not doing so defeats the purpose!");
        }
    }
}
