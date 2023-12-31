using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.IO
{
    public abstract class CloneableStream : Stream, ICloneable<CloneableStream>
    {
        public abstract CloneableStream Clone();
    }
}
