using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly ulong Position;
        public readonly ulong Length;

        public SngFileListing(YARGBinaryReader reader)
        {
            Length = reader.ReadUInt64();
            Position = reader.ReadUInt64();
        }
    }
}
