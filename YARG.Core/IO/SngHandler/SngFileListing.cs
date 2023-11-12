using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly long Position;
        public readonly long Length;

        public SngFileListing(YARGBinaryReader reader)
        {
            Length = reader.ReadInt64();
            Position = reader.ReadInt64();
        }
    }
}
