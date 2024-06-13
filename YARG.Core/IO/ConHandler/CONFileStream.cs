﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using YARG.Core.IO.Disposables;

namespace YARG.Core.IO
{
    public sealed class CONFileStream : Stream
    {
        private const int FIRSTBLOCK_OFFSET = 0xC000;
        private const int BYTES_PER_BLOCK = 0x1000;
        private const int BLOCKS_PER_SECTION = 170;
        private const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        private const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        private const int HASHBLOCK_OFFSET = 4075;
        private const int DIST_PER_HASH = 4072;

        public static AllocatedArray<byte> LoadFile(string file, bool isContinguous, int fileSize, int blockNum, int shift)
        {
            using FileStream filestream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return LoadFile(filestream, isContinguous, fileSize, blockNum, shift);
        }

        public static AllocatedArray<byte> LoadFile(Stream filestream, bool isContinguous, int fileSize, int blockNum, int shift)
        {
            var data = AllocatedArray<byte>.Alloc(fileSize);
            if (isContinguous)
            {
                long skipVal = BYTES_PER_BLOCK << shift;
                int threshold = blockNum - blockNum % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
                int numBlocks = BLOCKS_PER_SECTION - blockNum % BLOCKS_PER_SECTION;
                int readSize = BYTES_PER_BLOCK * numBlocks;
                int offset = 0;

                filestream.Seek(CalculateBlockLocation(blockNum, shift), SeekOrigin.Begin);
                while (true)
                {
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    if (filestream.Read(data.Slice(offset, readSize)) != readSize)
                    {
                        data.Dispose();
                        throw new Exception("Read error in CON-like subfile - Type: Contiguous");
                    }

                    offset += readSize;
                    if (offset == fileSize)
                        break;

                    blockNum += numBlocks;
                    numBlocks = BLOCKS_PER_SECTION;
                    readSize = BYTES_PER_SECTION;

                    int seekCount = 1;
                    if (blockNum == BLOCKS_PER_SECTION)
                        seekCount = 2;
                    else if (blockNum == threshold)
                    {
                        if (blockNum == NUM_BLOCKS_SQUARED)
                            seekCount = 2;
                        ++seekCount;
                        threshold += NUM_BLOCKS_SQUARED;
                    }

                    filestream.Seek(seekCount * skipVal, SeekOrigin.Current);
                }
            }
            else
            {
                Span<byte> buffer = stackalloc byte[3];
                int offset = 0;
                while (true)
                {
                    long blockLocation = CalculateBlockLocation(blockNum, shift);

                    int readSize = BYTES_PER_BLOCK;
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    filestream.Seek(blockLocation, SeekOrigin.Begin);
                    if (filestream.Read(data.Slice(offset, readSize)) != readSize)
                    {
                        data.Dispose();
                        throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
                    }

                    offset += readSize;
                    if (offset == fileSize)
                        break;

                    long hashlocation = blockLocation - ((long) (blockNum % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                    filestream.Seek(hashlocation, SeekOrigin.Begin);
                    if (filestream.Read(buffer) != buffer.Length)
                    {
                        data.Dispose();
                        throw new Exception("Post-Read error in CON-like subfile - Type: Split");
                    }
                    blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                }
            }
            return data;
        }

        public static long CalculateBlockLocation(int blockNum, int shift)
        {
            int blockAdjust = 0;
            if (blockNum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blockNum / BLOCKS_PER_SECTION + 1 << shift;
                if (blockNum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blockNum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return FIRSTBLOCK_OFFSET + (long) (blockAdjust + blockNum) * BYTES_PER_BLOCK;
        }

        private readonly FileStream _filestream;
        private readonly long fileSize;
        private readonly AllocatedArray<byte> dataBuffer;
        private readonly AllocatedArray<long> blockLocations;
        private readonly int initialOffset;

        private long bufferPosition;
        private long _position;
        private long blockIndex = 0;
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

                int truePosition = (int)(value + initialOffset);
                blockIndex = truePosition / dataBuffer.Length;
                bufferPosition = truePosition % dataBuffer.Length;

                long readSize = dataBuffer.Length - bufferPosition;
                if (readSize > fileSize - _position)
                    readSize = (int)(fileSize - _position);

                if (blockIndex < blockLocations.Length)
                {
                    long offset = blockIndex == 0 ? value : bufferPosition;
                    _filestream.Seek(blockLocations[(int)blockIndex++] + offset, SeekOrigin.Begin);
                    var buffer = dataBuffer.Slice(bufferPosition, readSize);
                    if (_filestream.Read(buffer) != readSize)
                        throw new Exception("Seek error in CON subfile");
                }
            }
        }

        public CONFileStream(string file, bool isContinguous, int fileSize, int firstBlock, int shift)
            : this(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1), isContinguous, fileSize, firstBlock, shift) { }

        public CONFileStream(FileStream filestream, bool isContinguous, int fileSize, int firstBlock, int shift)
        {
            _filestream = filestream;
            this.fileSize = fileSize;

            int block = firstBlock;
            if (isContinguous)
            {
                dataBuffer = AllocatedArray<byte>.Alloc(BYTES_PER_SECTION);

                int blockOffset = firstBlock % BLOCKS_PER_SECTION;
                initialOffset = blockOffset * BYTES_PER_BLOCK;

                int totalSpace = fileSize + initialOffset;
                int numBlocks = totalSpace % BYTES_PER_SECTION == 0 ? totalSpace / BYTES_PER_SECTION : totalSpace / BYTES_PER_SECTION + 1;
                blockLocations = AllocatedArray<long>.Alloc(numBlocks);

                int blockMovement = BLOCKS_PER_SECTION - blockOffset;
                int byteMovement = blockMovement * BYTES_PER_BLOCK;
                int skipVal = BYTES_PER_BLOCK << shift;
                int threshold = firstBlock - firstBlock % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
                long location = CalculateBlockLocation(firstBlock, shift);
                for (int i = 0; i < numBlocks; i++)
                {
                    blockLocations[i] = location;
                    if (i < numBlocks - 1)
                    {
                        block += blockMovement;

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

                        location += byteMovement + seekCount * skipVal;
                        blockMovement = BLOCKS_PER_SECTION;
                        byteMovement = BYTES_PER_SECTION;
                    }
                }
            }
            else
            {
                dataBuffer = AllocatedArray<byte>.Alloc(BYTES_PER_BLOCK);

                int numBlocks = fileSize % BYTES_PER_BLOCK == 0 ? fileSize / BYTES_PER_BLOCK : fileSize / BYTES_PER_BLOCK + 1;
                blockLocations = AllocatedArray<long>.Alloc(numBlocks);

                Span<byte> buffer = stackalloc byte[3];
                initialOffset = 0;
                for (int i = 0; i < numBlocks; i++)
                {
                    long location = CalculateBlockLocation(block, shift);
                    blockLocations[i] = location;

                    if (i < numBlocks - 1)
                    {
                        long hashlocation = location - ((long) (block % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                        _filestream.Seek(hashlocation, SeekOrigin.Begin);
                        if (_filestream.Read(buffer) != 3)
                            throw new Exception("Hashblock Read error in CON subfile");

                        block = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                    }
                }
            }
            UpdateBuffer();
        }

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
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer == null)
                throw new ArgumentNullException();

            if (buffer.Length < offset + count)
                throw new ArgumentException();

            if (_position == fileSize)
                return 0;

            long read = 0;
            long bytesLeftInSection = dataBuffer.Length - bufferPosition;
            if (bytesLeftInSection > fileSize - (int) _position)
                bytesLeftInSection = fileSize - (int) _position;

            while (read < count)
            {
                long readCount = count - read;
                if (readCount > bytesLeftInSection)
                    readCount = bytesLeftInSection;

                Unsafe.CopyBlock(ref buffer[offset + read], ref dataBuffer[(int)bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                bufferPosition += readCount;

                if (bufferPosition < dataBuffer.Length || _position == fileSize)
                    break;

                bytesLeftInSection = UpdateBuffer();
            }
            return (int)read;
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
                {
                    _filestream.Dispose();
                    dataBuffer.Dispose();
                    blockLocations.Dispose();
                }
                disposedStream = true;
            }
        }

        private long UpdateBuffer()
        {
            bufferPosition = blockIndex == 0 ? initialOffset : 0;
            long readSize = dataBuffer.Length - bufferPosition;
            if (readSize > fileSize - _position)
                readSize = fileSize - _position;

            _filestream.Seek(blockLocations[(int)blockIndex++], SeekOrigin.Begin);
            var buffer = dataBuffer.Slice(bufferPosition, readSize);
            if (_filestream.Read(buffer) != readSize)
                throw new Exception("Seek error in CON subfile");
            return readSize;
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
