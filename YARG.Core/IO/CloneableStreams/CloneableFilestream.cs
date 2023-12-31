using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.IO
{
    public class CloneableFilestream : CloneableStream
    {
        private readonly FileStream _filestream;

        public CloneableFilestream(FileStream filestream)
        {
            _filestream = filestream;
        }

        public override CloneableStream Clone()
        {
            var clonedFilestream = new FileStream(_filestream.Name, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return new CloneableFilestream(clonedFilestream);
        }

        public override bool CanRead => _filestream.CanRead;

        public override bool CanSeek => _filestream.CanSeek;

        public override bool CanWrite => _filestream.CanWrite;

        public override long Length => _filestream.Length;

        public override long Position { get => _filestream.Position; set => _filestream.Position = value; }

        public override void Flush()
        {
            _filestream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _filestream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _filestream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _filestream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _filestream.Write(buffer, offset, count);
        }
    }
}
