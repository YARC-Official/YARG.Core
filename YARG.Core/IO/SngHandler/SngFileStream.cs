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
        private static readonly int NUM_VECTORS_MASK = SngMask.NUMVECTORS - 1;
        private static readonly int VECTOR_SHIFT;
        private static readonly int VECTOR_INDEX_MASK;

        static SngFileStream()
        {
            int val = SngMask.VECTORBYTE_COUNT;
            while (val > 1)
            {
                VECTOR_SHIFT++;
                val >>= 1;
            }
            VECTOR_INDEX_MASK = NUM_VECTORS_MASK << VECTOR_SHIFT;
        }

        public static byte[] LoadFile(FileStream stream, SngMask mask, long fileSize, long position)
        {
            if (stream.Seek(position, SeekOrigin.Begin) != position)
                throw new EndOfStreamException();

            byte[] buffer = stream.ReadBytes((int)fileSize);
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    var buffEnd = ptr + buffer.Length;
                    var buffIndex = buffer.Length & ~VECTOR_MASK;
                    var buffPosition = ptr + buffIndex;

                    var vecPtr = (Vector<byte>*) ptr;
                    Parallel.For(0, SngMask.NUMVECTORS, i =>
                    {
                        var xor = mask.Vectors[i];
                        for (var loc = vecPtr + i; loc < buffPosition; loc += SngMask.NUMVECTORS)
                        {
                            *loc ^= xor;
                        }
                    });

                    long keyIndex = buffIndex & KEY_MASK;
                    while (buffPosition < buffEnd)
                    {
                        *buffPosition++ ^= mask.Keys.Ptr[keyIndex++];
                    }
                }
            }
            return buffer;
        }

        // 128kiB
        private const int BUFFER_SIZE = 128 * 1024;
        private const int SEEK_MODULUS = BUFFER_SIZE - 1;
        private const int SEEK_MODULUS_MINUS = ~SEEK_MODULUS;

        private readonly FileStream _stream;
        private readonly long fileSize;
        private readonly long initialOffset;

        private readonly SngMask mask;
        private readonly FixedArray<byte> dataBuffer = FixedArray<byte>.Alloc(BUFFER_SIZE);

        public  readonly string Name;


        private int bufferPosition;
        private long _position;
        private bool disposedStream;

        public override bool CanRead => _stream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => _stream.CanSeek;
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

                _stream.Seek(_position + initialOffset, SeekOrigin.Begin);
                bufferPosition = (int)(value & SEEK_MODULUS);
                UpdateBuffer();
            }
        }

        public SngFileStream(string name, FileStream stream, SngMask mask, long fileSize, long position)
        {
            Name = name;
            _stream = stream;

            this.fileSize = fileSize;
            this.mask = mask;

            initialOffset = position;

            _stream.Seek(position, SeekOrigin.Begin);
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
            long bytesLeftInSection = dataBuffer.Length - bufferPosition;
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

                if (bufferPosition < dataBuffer.Length || _position == fileSize)
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
            _stream.Flush();
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
                    _stream.Dispose();
                    dataBuffer.Dispose();
                    mask.Dispose();
                }
                disposedStream = true;
            }
        }


        private unsafe long UpdateBuffer()
        {
            int readCount = BUFFER_SIZE - bufferPosition;
            if (readCount > fileSize - _position)
                readCount = (int)(fileSize - _position);

            var buffer = dataBuffer.Slice(bufferPosition, readCount);
            if (_stream.Read(buffer) != readCount)
                throw new Exception("Seek error in SNGPKG subfile");

            int buffIndex = bufferPosition;
            int count = SngMask.VECTORBYTE_COUNT - (buffIndex & VECTOR_MASK);
            if (count > readCount)
                count = readCount;

            // Line up to a vector boundary
            if (count != SngMask.VECTORBYTE_COUNT)
            {
                int key = buffIndex & KEY_MASK;
                for (int i = 0; i < count; ++i)
                {
                    dataBuffer.Ptr[buffIndex++] ^= mask.Keys.Ptr[key++];
                }

                // No need to do anything else
                if (count == readCount)
                {
                    return readCount;
                }
            }

            int endIndex = bufferPosition + readCount;
            var endPtr = dataBuffer.Ptr + endIndex;

            int vectorIndex = (buffIndex & VECTOR_INDEX_MASK) >> VECTOR_SHIFT;
            int vectorMax = endIndex & ~VECTOR_MASK;
            
            var buffPosition = dataBuffer.Ptr + vectorMax;
            var vecPtr = (Vector<byte>*) (dataBuffer.Ptr + buffIndex);
            Parallel.For(0, SngMask.NUMVECTORS, i =>
            {
                // Faster "% NUM_VECTORS"
                var xor = mask.Vectors[(i + vectorIndex) & NUM_VECTORS_MASK];
                for (var loc = vecPtr + i; loc < buffPosition; loc += SngMask.NUMVECTORS)
                {
                    *loc ^= xor;
                }
            });

            long keyIndex = vectorMax & KEY_MASK;
            while (buffPosition < endPtr)
            {
                *buffPosition++ ^= mask.Keys.Ptr[keyIndex++];
            }
            return readCount;
        }
    }
}
