using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly long Position;
        public readonly long Length;

        public SngFileListing(YARGBinaryReader reader)
        {
            Length = reader.Read<long>();
            Position = reader.Read<long>();
        }

        public byte[] LoadAllBytes(string filename, SngMask mask)
        {
            return SngFileStream.LoadFile(filename, Length, Position, mask.Clone());
        }

        public SngFileStream CreateStream(string filename, SngMask mask)
        {
            return new SngFileStream(filename, Length, Position, mask.Clone());
        }
    }
}
