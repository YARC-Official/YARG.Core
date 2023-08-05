using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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

        public byte[] CalcSHA()
        {
            return SHA1.Create().ComputeHash((byte[]) handle.Target);
        }

        public byte[] CalcMD5()
        {
            return MD5.Create().ComputeHash((byte[]) handle.Target);
        }
    }
}
