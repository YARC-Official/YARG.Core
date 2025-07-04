using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace YARG.Core.IO
{
    public class SngFileStream : Stream
    {
        //                1MB
        private const int BUFFER_SIZE = 1024 * 1024;
        private const int SEEK_MODULUS = BUFFER_SIZE - 1;

        private readonly SngTracker _tracker;
        private readonly string _filename;
        private readonly SngFileListing _listing;
        private readonly FixedArray<byte> _dataBuffer = FixedArray<byte>.AllocVectorAligned(BUFFER_SIZE);

        private int  _bufferIndex;
        private int  _bufferPosition;
        private int  _position;

        public override bool CanRead => _tracker.Stream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => _tracker.Stream.CanSeek;
        public override long Length => _listing.Length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _listing.Length)
                {
                    throw new ArgumentOutOfRangeException();
                }

                _position = (int)value;
                long index = _position / BUFFER_SIZE;
                if (_bufferIndex != index)
                {
                    _bufferIndex = -1;
                }
                else
                {
                    _bufferPosition = _position % BUFFER_SIZE;
                }
            }
        }

        public string Name => _filename;

        public SngFileStream(string name, in SngFileListing listing, SngTracker tracker)
        {
            _filename = name;
            _listing = listing;
            _tracker = tracker.AddOwner();
            _bufferIndex = -1;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (buffer == null)
            {
                throw new ArgumentNullException();
            }

            if (buffer.Length < offset + count)
            {
                throw new ArgumentException();
            }

            if (_position == _listing.Length)
            {
                return 0;
            }

            int read = 0;
            while (read < count && _position < _listing.Length)
            {
                if (_bufferIndex == -1 ||_bufferPosition == BUFFER_SIZE)
                {
                    UpdateBuffer();
                }

                int available = BUFFER_SIZE - _bufferPosition;
                long remainingInFile = _listing.Length - _position;
                if (available > remainingInFile)
                {
                    available = (int)remainingInFile;
                }

                int amount = count - read;
                if (amount > available)
                {
                    amount = available;
                }

                Unsafe.CopyBlock(ref buffer[offset + read], ref _dataBuffer[_bufferPosition], (uint) amount);
                read += amount;
                _position += amount;
                _bufferPosition += amount;
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
                    Position = _listing.Length + offset;
                    break;
            }
            return _position;
        }

        public override void Flush()
        {
            lock (_tracker.Stream)
            {
                _tracker.Stream.Flush();
            }
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
            if (disposing)
            {
                _dataBuffer.Dispose();
                _tracker.Dispose();
            }
        }

        // We make a local copy to grant direct access to the Keys pointer
        // without having to make a `fixed` call
        private unsafe void UpdateBuffer()
        {
            _bufferPosition = _position % BUFFER_SIZE;
            int index = _position / BUFFER_SIZE;
            if (index == _bufferIndex)
            {
                return;
            }
            _bufferIndex = index;

            long readCount = BUFFER_SIZE;
            long readPosition = _position - _bufferPosition;
            if (readCount > _listing.Length - readPosition)
            {
                readCount = _listing.Length - readPosition;
            }

            lock (_tracker.Stream)
            {
                _tracker.Stream.Position = readPosition + _listing.Position;
                if (_tracker.Stream.Read(_dataBuffer[..(int)readCount]) != readCount)
                {
                    throw new IOException("Read error in SNGPKG subfile");
                }
            }
            DecryptVectorized(_dataBuffer.Ptr, _tracker.Mask, _dataBuffer.Ptr + readCount);
        }

        public static unsafe void DecryptVectorized(byte* position, SngMask mask, byte* end)
        {
            byte* keyPosition = mask.Ptr;
            Parallel.For(0, SngMask.NUM_VECTORS, i =>
            {
                var xor = *((Vector<byte>*) keyPosition + i);
                for (var loc = (Vector<byte>*) position + i; loc + 1 <= end; loc += SngMask.NUM_VECTORS)
                {
                    *loc ^= xor;
                }
            });

            long numVecs = (end - position) / sizeof(Vector<byte>);
            position += numVecs * sizeof(Vector<byte>);
            keyPosition += (numVecs % SngMask.NUM_VECTORS) * sizeof(Vector<byte>);

            while (position < end)
            {
                *position++ ^= *keyPosition++;
            }
        }
    }
}
