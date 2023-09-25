using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace YARG.Core.IO
{
    public class CONFileStream : Stream
    {
        private const int FIRSTBLOCK_OFFSET = 0xC000;
        private const int BYTES_PER_BLOCK = 0x1000;
        private const int BLOCKS_PER_SECTION = 170;
        private const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        private const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        private const int HASHBLOCK_OFFSET = 4075;
        private const int DIST_PER_HASH = 4072;

        private readonly FileStream _filestream;
        private readonly int fileSize;
        private readonly byte[] dataBuffer;
        private readonly long[] blockLocations;
        private readonly int initialOffset;

        private int bufferPosition;
        private long _position;
        private int blockIndex = 0;
        private bool disposedStream;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => true;
        public override long Length => fileSize;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > fileSize) throw new ArgumentOutOfRangeException();

                _position = value;
                blockIndex = (int) (value / dataBuffer.Length);

                int offset = blockIndex == 0 ? initialOffset : 0;
                bufferPosition = (int) (value % dataBuffer.Length) + offset;
                
                long readPosition = value - bufferPosition;
                long readSize = dataBuffer.LongLength - offset;
                if (readSize > fileSize - readPosition)
                    readSize = fileSize - readPosition;

                if (blockIndex < blockLocations.Length)
                {
                    _filestream.Seek(blockLocations[blockIndex++], SeekOrigin.Begin);
                    if (_filestream.Read(dataBuffer, offset, (int) readSize) != readSize)
                        throw new Exception("Read error in CON subfile");
                }
            }
        }

        public CONFileStream(FileStream filestream, bool isContinguous, int fileSize, int firstBlock, int shift)
        {
            _filestream = filestream;
            this.fileSize = fileSize;

            int block = firstBlock;
            if (isContinguous)
            {
                dataBuffer = new byte[BYTES_PER_SECTION];

                int blockOffset = firstBlock % BLOCKS_PER_SECTION;
                initialOffset = blockOffset * BYTES_PER_BLOCK;

                int totalSpace = fileSize + initialOffset;
                int numBlocks = totalSpace % BYTES_PER_SECTION == 0 ? totalSpace / BYTES_PER_SECTION : totalSpace / BYTES_PER_SECTION + 1;
                blockLocations = new long[numBlocks];

                int blockMovement = BLOCKS_PER_SECTION - blockOffset;
                int byteMovement = blockMovement * BYTES_PER_BLOCK;
                int skipVal = BYTES_PER_BLOCK << shift;
                int threshold = firstBlock - firstBlock % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
                long location = FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(block, shift) * BYTES_PER_BLOCK;
                for (int i = 0; i < numBlocks; i++)
                {
                    blockLocations[i] = location;
                    if (i < numBlocks - 1)
                    {
                        int seekCount = 1;
                        if (block == BLOCKS_PER_SECTION)
                            seekCount = 2;
                        else if (block == threshold)
                        {
                            if (block == NUM_BLOCKS_SQUARED)
                                seekCount = 2;
                            ++seekCount;
                            threshold += NUM_BLOCKS_SQUARED;
                        }

                        block += blockMovement;
                        location += byteMovement + seekCount * skipVal;
                        blockMovement = BLOCKS_PER_SECTION;
                        byteMovement = BYTES_PER_SECTION;
                    }
                }
            }
            else
            {
                dataBuffer = new byte[BYTES_PER_BLOCK];

                int numBlocks = fileSize % BYTES_PER_BLOCK == 0 ? fileSize / BYTES_PER_BLOCK : fileSize / BYTES_PER_BLOCK + 1;
                blockLocations = new long[numBlocks];

                initialOffset = 0;
                for (int i = 0; i < numBlocks; i++)
                {
                    long location = FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(block, shift) * BYTES_PER_BLOCK;
                    blockLocations[i] = location;

                    if (i < numBlocks - 1)
                    {
                        long hashlocation = location - ((long) (block % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                        Span<byte> buffer = stackalloc byte[3];
                        _filestream.Seek(hashlocation, SeekOrigin.Begin);
                        if (_filestream.Read(buffer) != 3)
                            throw new Exception("Hashblock Read error in CON subfile");

                        block = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                    }
                }
            }
            UpdateBuffer();
        }

        public CONFileStream(FileStream filestream, CONFileListing listing, int shift)
            : this(filestream, listing.IsContiguous(), listing.size, listing.firstBlock, shift) { }

        public override void Flush()
        {
            _filestream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == fileSize)
                return 0;

            int read = 0;
            int bytesLeftInSection = dataBuffer.Length - bufferPosition;
            if (bytesLeftInSection > fileSize - (int) _position)
                bytesLeftInSection = fileSize - (int) _position;

            while (true)
            {
                int readCount = count - read;
                if (readCount > bytesLeftInSection)
                    readCount = bytesLeftInSection;

                Unsafe.CopyBlock(ref buffer[offset], ref dataBuffer[bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                bufferPosition += readCount;

                if (bufferPosition < dataBuffer.Length || _position == fileSize)
                    break;

                offset += readCount;
                bytesLeftInSection = UpdateBuffer();
            }
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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

        protected override void Dispose(bool disposing)
        {
            if (!disposedStream)
            {
                if (disposing)
                    _filestream.Dispose();
                disposedStream = true;
            }
        }

        private int UpdateBuffer()
        {
            bufferPosition = blockIndex == 0 ? initialOffset : 0;
            long readSize = dataBuffer.Length - bufferPosition;
            if (readSize > fileSize - _position)
                readSize = fileSize - _position;

            _filestream.Seek(blockLocations[blockIndex++], SeekOrigin.Begin);
            if (_filestream.Read(dataBuffer, bufferPosition, (int) readSize) != readSize)
                throw new Exception("Read error in CON subfile");
            return (int) readSize;
        }

        private static int CalculateBlockNum(int blockNum, int shift)
        {
            int blockAdjust = 0;
            if (blockNum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blockNum / BLOCKS_PER_SECTION + 1 << shift;
                if (blockNum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blockNum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return blockAdjust + blockNum;
        }
    }
}
