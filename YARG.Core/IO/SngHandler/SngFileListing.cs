using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly string Filename;
        public readonly ulong Position;
        public readonly ulong Length;

        public SngFileListing(YARGBinaryReader reader, ulong posOffset)
        {
            var strLen = reader.ReadByte();
            Filename = Encoding.UTF8.GetString(reader.ReadBytes(strLen));
            Length = reader.ReadUInt64();
            Position = reader.ReadUInt64() + posOffset;
        }
    }
}
