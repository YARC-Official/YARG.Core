using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileStream : Stream
    {
        private const int NUM_KEYBYTES = 256;
        private const int MASKLENGTH = 16;

        public static byte[] LoadFile(string file, long fileSize, long position, SngMask mask)
        {
            using FileStream filestream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (filestream.Seek(position, SeekOrigin.Begin) != position)
                throw new EndOfStreamException();

            byte[] buffer = filestream.ReadBytes((int)fileSize);

            int buffIndex = 0;
            int vectorMax = buffer.Length - SngMask.VECTORBYTE_COUNT;
            for (int vectorIndex = 0; buffIndex <= vectorMax;)
            {
                var vec = new Vector<byte>(buffer, buffIndex);
                unsafe
                {
                    var result = Vector.Xor(vec, mask.Vectors[vectorIndex++]);
                    result.CopyTo(buffer, buffIndex);
                }
                buffIndex += SngMask.VECTORBYTE_COUNT;

                if (vectorIndex == SngMask.NUMVECTORS)
                    vectorIndex = 0;
            }

            while (buffIndex < buffer.Length)
            {
                buffer[buffIndex] ^= mask.Keys[buffIndex & 255];
                buffIndex++;
            }
            return buffer;
        }

        private const int BUFFER_SIZE = 4096;
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


        private static readonly int VECTOR_MASK = SngMask.VECTORBYTE_COUNT - 1;
        private static readonly int VECTOR_SHIFT;
        private const int KEY_MASK = NUM_KEYBYTES - 1;

        static SngFileStream()
        {
            int val = SngMask.VECTORBYTE_COUNT;
            while (val > 1)
            {
                VECTOR_SHIFT++;
                val >>= 1;
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

            while (buffIndex <= vectorMax)
            {
                var vec = new Vector<byte>(dataBuffer.Slice(buffIndex, SngMask.VECTORBYTE_COUNT));
                unsafe
                {
                    var result = Vector.Xor(vec, mask.Vectors[vectorIndex++]);
                    Unsafe.CopyBlock(dataBuffer.Ptr + buffIndex, &result, (uint)SngMask.VECTORBYTE_COUNT);
                }
                buffIndex += SngMask.VECTORBYTE_COUNT;

                if (vectorIndex == SngMask.NUMVECTORS)
                    vectorIndex = 0;
            }

            while (buffIndex < end)
            {
                dataBuffer[buffIndex] ^= mask.Keys[buffIndex & 255];
                buffIndex++;
            }

            return readCount;
        }
    }
}
