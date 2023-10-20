using System;
using System.IO;

namespace YARG.Core.IO
{
    public class SharedCONStream : IDisposable
    {
        public readonly FileStream stream;
        public readonly object Lock = new();

        public SharedCONStream(FileStream stream)
        {
            this.stream = stream;
        }

        public SharedCONStream(string file)
        {
            stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
