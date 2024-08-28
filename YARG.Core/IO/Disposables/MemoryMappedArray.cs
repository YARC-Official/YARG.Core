using System.IO;
using System.IO.MemoryMappedFiles;

namespace YARG.Core.IO.Disposables
{
    public sealed unsafe class MemoryMappedArray : FixedArray<byte>
    {
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _accessor;

        private MemoryMappedArray(MemoryMappedFile file, MemoryMappedViewAccessor accessor, byte* ptr, long length)
            : base(ptr, length)
        {
            _file = file;
            _accessor = accessor;
        }

        // Note: not DisposeUnmanaged, as object references are not guaranteed to be valid during finalization
        protected override void DisposeManaged()
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _file.Dispose();
        }

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
    }
}
