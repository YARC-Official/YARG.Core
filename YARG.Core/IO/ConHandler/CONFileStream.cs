﻿using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace YARG.Core.IO
{
    public abstract class CONFileStream : Stream
    {
        protected const int FIRSTBLOCK_OFFSET = 0xC000;
        protected const int BYTES_PER_BLOCK = 0x1000;
        protected const int BLOCKS_PER_SECTION = 170;
        protected const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        protected readonly FileStream _filestream;
        protected readonly int fileSize;
        protected readonly int firstblock;
        protected readonly int shift;

        protected int currentBlock;
        protected int bufferPosition;
        protected long _position;
        private bool disposedStream;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => true;
        public override long Length => fileSize;

        protected CONFileStream(FileStream filestream, int fileSize, int firstBlock, int shift)
        {
            _filestream = filestream;
            this.shift = shift;
            this.fileSize = fileSize;
            currentBlock = firstblock = firstBlock;
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

        protected int CalculateBlockNum(int blockNum)
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

    public class ContiguousCONFileStream : CONFileStream
    {
        protected const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;

        private readonly long skipVal;
        private readonly byte[] sectionBuffer = new byte[BYTES_PER_SECTION];
        private int threshold;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > fileSize) throw new ArgumentOutOfRangeException();

                currentBlock = firstblock;
                long seekPosition = FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(currentBlock) * BYTES_PER_BLOCK;
                int positionLeft = (int) value;

                int offsetBlocks = currentBlock % BLOCKS_PER_SECTION;
                bufferPosition = BYTES_PER_BLOCK * offsetBlocks;
                currentBlock -= offsetBlocks;

                int moveAmount = BYTES_PER_SECTION - bufferPosition;
                int skipCount = 0;
                while (positionLeft >= moveAmount)
                {
                    seekPosition += moveAmount;
                    positionLeft -= moveAmount;
                    skipCount += CalcSkipCount();
                    moveAmount = BYTES_PER_SECTION;

                    bufferPosition = 0;
                    currentBlock += BLOCKS_PER_SECTION;
                }

                _filestream.Seek(seekPosition + skipCount * skipVal + positionLeft, SeekOrigin.Begin);

                long readCount = BYTES_PER_SECTION - bufferPosition;
                if (readCount > fileSize - value)
                    readCount = fileSize - value;

                _filestream.Read(sectionBuffer, bufferPosition, (int)readCount);
                bufferPosition += positionLeft;
                _position = value;
            }
        }

        public ContiguousCONFileStream(FileStream filestream, int fileSize, int firstBlock, int shift)
            : base(filestream, fileSize, firstBlock, shift)
        {
            skipVal = BYTES_PER_BLOCK << shift;

            threshold = currentBlock - currentBlock % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;

            int offsetBlocks = currentBlock % BLOCKS_PER_SECTION;
            bufferPosition = BYTES_PER_BLOCK * offsetBlocks;
            int readSize = BYTES_PER_SECTION - bufferPosition;

            _filestream.Seek(FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(currentBlock) * BYTES_PER_BLOCK, SeekOrigin.Begin);
            if (readSize > fileSize)
                readSize = fileSize;

            if (_filestream.Read(sectionBuffer, bufferPosition, readSize) != readSize)
                throw new Exception("Read error in CON-like subfile - Type: Contiguous");

            currentBlock -= offsetBlocks;
        }

        public ContiguousCONFileStream(string filename, int fileSize, int firstBlock, int shift)
            : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1), fileSize, firstBlock, shift)
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == fileSize)
                return 0;

            int read = 0;
            int bytesLeftInSection = BYTES_PER_SECTION - bufferPosition;
            int fileLeft = fileSize - (int)_position;
            if (bytesLeftInSection > fileLeft)
                bytesLeftInSection = fileLeft;

            while (true)
            {
                int readCount = count - read;
                if (readCount > bytesLeftInSection)
                    readCount = bytesLeftInSection;

                Unsafe.CopyBlock(ref buffer[offset], ref sectionBuffer[bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                bufferPosition += readCount;

                if (bufferPosition < BYTES_PER_SECTION || _position == fileSize)
                    break;

                offset += readCount;
                fileLeft -= readCount;
                
                bufferPosition = 0;
                currentBlock += BLOCKS_PER_SECTION;
                _filestream.Seek(CalcSkipCount() * skipVal, SeekOrigin.Current);

                readCount = BYTES_PER_SECTION;
                if (readCount > fileLeft)
                    readCount = fileLeft;

                _filestream.Read(sectionBuffer, 0, readCount);
                bytesLeftInSection = readCount;
            }
            return read;
        }

        private int CalcSkipCount()
        {
            int seekCount = 1;
            if (currentBlock == BLOCKS_PER_SECTION)
                seekCount = 2;
            else if (currentBlock == threshold)
            {
                if (currentBlock == NUM_BLOCKS_SQUARED)
                    seekCount = 2;
                ++seekCount;
                threshold += NUM_BLOCKS_SQUARED;
            }

            return seekCount;
        }
    }

    public class SplitCONFileStream : CONFileStream
    {
        private const int HASHBLOCK_OFFSET = 4075;
        private const int DIST_PER_HASH = 4072;

        private readonly byte[] blockBuffer = new byte[BYTES_PER_BLOCK];
        private readonly long[] blockLocations;
        private int blockIndex = 0;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > fileSize) throw new ArgumentOutOfRangeException();

                blockIndex = (int) (value / BYTES_PER_BLOCK);
                bufferPosition = (int) (value % BYTES_PER_BLOCK);
                _position = value;

                long readPosition = value - bufferPosition;
                long readSize = BYTES_PER_BLOCK;
                if (readSize > fileSize - readPosition)
                    readSize = fileSize - readPosition;

                _filestream.Seek(blockLocations[blockIndex++], SeekOrigin.Begin);
                if (_filestream.Read(blockBuffer, 0, (int) readSize) != readSize)
                    throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
            }
        }

        public SplitCONFileStream(string filename, int fileSize, int firstBlock, int shift)
            : base(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1), fileSize, firstBlock, shift)
        {
            int numBlocks = fileSize % BYTES_PER_BLOCK == 0 ? fileSize / BYTES_PER_BLOCK : fileSize / BYTES_PER_BLOCK + 1;
            blockLocations = new long[numBlocks];

            int block = firstBlock;
            for (int i = 0; i < numBlocks; i++)
            {
                long location = FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(block) * BYTES_PER_BLOCK; ;
                blockLocations[i] = location;

                if (i < numBlocks - 1)
                {
                    long hashlocation = location - ((long) (block % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                    Span<byte> buffer = stackalloc byte[3];
                    _filestream.Seek(hashlocation, SeekOrigin.Begin);
                    if (_filestream.Read(buffer) != 3)
                        throw new Exception("Post-Read error in CON-like subfile - Type: Split");

                    block = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                }
            }

            UpdateBuffer();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == fileSize)
                return 0;

            int read = 0;
            int leftoverBytes = BYTES_PER_BLOCK - bufferPosition;
            int fileLeft = fileSize - (int) _position;
            if (leftoverBytes > fileLeft)
                leftoverBytes = fileLeft;

            while (true)
            {
                int readCount = count - read;
                if (readCount > leftoverBytes)
                    readCount = leftoverBytes;

                Unsafe.CopyBlock(ref buffer[offset], ref blockBuffer[bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                bufferPosition += readCount;

                if (bufferPosition < BYTES_PER_BLOCK || _position == fileSize)
                    break;

                offset += readCount;
                fileLeft -= readCount;
                UpdateBuffer();

                leftoverBytes = fileLeft >= BYTES_PER_BLOCK ? BYTES_PER_BLOCK : fileLeft;
            }
            return read;
        }


        private void UpdateBuffer()
        {
            long readSize = BYTES_PER_BLOCK;
            if (readSize > fileSize - _position)
                readSize = fileSize - _position;

            _filestream.Seek(blockLocations[blockIndex++], SeekOrigin.Begin);
            if (_filestream.Read(blockBuffer, 0, (int)readSize) != readSize)
                throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
            bufferPosition = 0;
        }
    }
}
