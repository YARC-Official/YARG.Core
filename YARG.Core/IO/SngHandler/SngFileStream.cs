using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileStream : Stream
    {
        private const int KEY_MASK = 0xFF;

        private static readonly int VECTOR_MASK = SngMask.VECTORBYTE_COUNT - 1;
        private static readonly int VECTOR_SHIFT;
        private static readonly int NUM_VECTORS_MASK = SngMask.NUMVECTORS - 1;

        static SngFileStream()
        {
            int val = SngMask.VECTORBYTE_COUNT;
            while (val > 1)
            {
                VECTOR_SHIFT++;
                val >>= 1;
            }
        }

        public static byte[] LoadFile(string file, long fileSize, long position, SngMask mask)
        {
            using FileStream filestream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (filestream.Seek(position, SeekOrigin.Begin) != position)
                throw new EndOfStreamException();

            byte[] buffer = filestream.ReadBytes((int)fileSize);
            int loopCount = (buffer.Length - SngMask.VECTORBYTE_COUNT) >> VECTOR_SHIFT;
            Parallel.For(0, loopCount, i =>
            {
                int loc = (i << VECTOR_SHIFT);
                var vec = new Vector<byte>(buffer, loc);
                unsafe
                {
                    var result = Vector.Xor(vec, mask.Vectors[i & NUM_VECTORS_MASK]);
                    result.CopyTo(buffer, loc);
                }
            });

            for (int buffIndex = loopCount << VECTOR_SHIFT; buffIndex < buffer.Length; buffIndex++)
                buffer[buffIndex] ^= mask.Keys[buffIndex & KEY_MASK];
            return buffer;
        }

        // 128kiB
        private const int BUFFER_SIZE = 128 * 1024;
        private const int SEEK_MODULUS = BUFFER_SIZE - 1;
        private const int SEEK_MODULUS_MINUS = ~SEEK_MODULUS;
        

        private readonly FileStream _filestream;
        private readonly long fileSize;
        private readonly long initialOffset;

        private readonly SngMask mask;
        private readonly DisposableArray<byte> dataBuffer = new(BUFFER_SIZE);
        

        private int bufferPosition;
        private long _position;
        private bool disposedStream;

        public override bool CanRead => _filestream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => _filestream.CanSeek;
        public override long Length => fileSize;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > fileSize) throw new ArgumentOutOfRangeException();

                _position = value;
                if (value == fileSize)
                    return;

                _filestream.Seek(_position + initialOffset, SeekOrigin.Begin);
                bufferPosition = (int)(value & SEEK_MODULUS);
                UpdateBuffer();
            }
        }

        public SngFileStream(string file, long fileSize, long position, SngMask mask)
        {
            _filestream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            this.fileSize = fileSize;
            this.mask = mask;
            initialOffset = position;
            _filestream.Seek(position, SeekOrigin.Begin);
            UpdateBuffer();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer == null)
                throw new ArgumentNullException();

            if (buffer.Length < offset + count)
                throw new ArgumentException();

            if (_position == fileSize)
                return 0;

            int read = 0;
            long bytesLeftInSection = dataBuffer.Size - bufferPosition;
            if (bytesLeftInSection > fileSize - _position)
                bytesLeftInSection = fileSize - _position;

            while (read < count)
            {
                int readCount = count - read;
                if (readCount > bytesLeftInSection)
                    readCount = (int)bytesLeftInSection;

                Unsafe.CopyBlock(ref buffer[offset + read], ref dataBuffer[bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                bufferPosition += readCount;

                if (bufferPosition < dataBuffer.Size || _position == fileSize)
                    break;

                bufferPosition = 0;
                bytesLeftInSection = UpdateBuffer();
            }
            return read;
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = fileSize + offset;
                    break;
            }
            return _position;
        }

        public override void Flush()
        {
            _filestream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedStream)
            {
                if (disposing)
                {
                    _filestream.Dispose();
                    dataBuffer.Dispose();
                    mask.Dispose();
                }
                disposedStream = true;
            }
        }
        

        private long UpdateBuffer()
        {
            int readCount = BUFFER_SIZE - bufferPosition;
            if (readCount > fileSize - _position)
                readCount = (int)(fileSize - _position);

            var buffer = dataBuffer.Slice(bufferPosition, readCount);
            if (_filestream.Read(buffer) != readCount)
                throw new Exception("Seek error in SNGPKG subfile");

            int buffIndex = bufferPosition;
            int key = buffIndex & KEY_MASK;
            {
                int count = SngMask.VECTORBYTE_COUNT - (buffIndex & VECTOR_MASK);
                if (count > readCount)
                    count = readCount;

                if (count != SngMask.VECTORBYTE_COUNT)
                {
                    for (int i = 0; i < count; ++i)
                        dataBuffer[buffIndex++] ^= mask.Keys[key++];

                    if (key == 256)
                        key = 0;
                }
            }

            int vectorIndex = (key & ~VECTOR_MASK) >> VECTOR_SHIFT;
            int end = bufferPosition + readCount;
            int vectorMax = end - SngMask.VECTORBYTE_COUNT;
            int loopCount = (vectorMax - buffIndex) >> VECTOR_SHIFT;

            Parallel.For(0, loopCount, i =>
            {
                int loc = (i << VECTOR_SHIFT) + buffIndex;
                var vec = new Vector<byte>(dataBuffer.Slice(loc, SngMask.VECTORBYTE_COUNT));
                unsafe
                {
                    var result = Vector.Xor(vec, mask.Vectors[(vectorIndex + i) & NUM_VECTORS_MASK]);
                    Unsafe.CopyBlock(dataBuffer.Ptr + loc, &result, (uint) SngMask.VECTORBYTE_COUNT);
                }
            });

            buffIndex += loopCount << VECTOR_SHIFT;
            while (buffIndex < end)
            {
                dataBuffer[buffIndex] ^= mask.Keys[buffIndex & 255];
                buffIndex++;
            }

            return readCount;
        }
    }
}
