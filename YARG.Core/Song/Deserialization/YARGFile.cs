using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using YARG.Core.Song.Metadata;

namespace YARG.Core.Song.Deserialization
{
    public unsafe class YARGFile
    {
        private readonly GCHandle handle;
        private readonly byte* _data;
        private readonly int _length;

        public byte* Data => _data;
        public int Length => _length;


        protected YARGFile() { }

        public YARGFile(byte[] data)
        {
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            _data = (byte*) handle.AddrOfPinnedObject();
            _length = data.Length;
        }

        public YARGFile(string file) : this(File.ReadAllBytes(file)) { }

        ~YARGFile()
        {
            handle.Free();
        }

        public byte[] CalcHash()
        {
            return HashWrapper.Algorithm.ComputeHash((byte[]) handle.Target);
        }
    }
}
