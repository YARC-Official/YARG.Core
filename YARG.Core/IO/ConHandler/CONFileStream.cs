using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace YARG.Core.IO
{
    public sealed class CONFileStream : Stream
    {
        public const long FIRSTBLOCK_OFFSET = 0xC000;
        public const long BYTES_PER_BLOCK = 0x1000;
        public const long BLOCKS_PER_SECTION = 170;
        public const long BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        public const long NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        public const long BYTES_PER_HASH_ENTRY = 0x18;
        public const long NEXT_BLOCK_HASH_OFFSET = 0x15;
        public const long HASHBLOCK_OFFSET = 4075;
        public const long DIST_PER_HASH = 4072;

        private readonly FileStream _filestream;
        private readonly long _length;
        private readonly long _initialOffset;
        private readonly FixedArray<byte> _dataBuffer;
        private readonly FixedArray<long> _blockLocations;

        private long _bufferPosition;
        private long _position;
        private long _bufferIndex = -1;

        public override bool CanRead => _filestream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => _filestream.CanSeek;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException();
                }

                _position = value;
                long truePosition = _position + _initialOffset;
                long index = truePosition / _dataBuffer.Length;
                if (_bufferIndex != index)
                {
                    _bufferIndex = -1;
                }
                else
                {
                    _bufferPosition = truePosition % _dataBuffer.Length;
                }
            }
        }

        public override void Flush()
        {
            _filestream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Not allowed to set stream length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Not allowed to write to stream");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long read = 0;
            while (read < count && _position < _length)
            {
                if (_bufferIndex == -1 || _bufferPosition == _dataBuffer.Length)
                {
                    UpdateBuffer();
                }

                long available = _dataBuffer.Length - _bufferPosition;
                long remainingInFile = _length - _position;
                if (available > remainingInFile)
                {
                    available = remainingInFile;
                }

                long amount = count - read;
                if (amount > available)
                {
                    amount = available;
                }

                Unsafe.CopyBlock(ref buffer[offset + read], ref _dataBuffer[_bufferPosition], (uint) amount);

                read += amount;
                _position += amount;
                _bufferPosition += amount;
            }
            return (int) read;
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
                    Position = _length + offset;
                    break;
            }
            return _position;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _filestream.Dispose();
            }
            _dataBuffer.Dispose();
            _blockLocations.Dispose();
        }

        private void UpdateBuffer()
        {
            long truePosition = _position + _initialOffset;
            long index = truePosition / _dataBuffer.Length;
            if (index == _bufferIndex)
            {
                return;
            }

            _bufferIndex = index;
            _filestream.Position = _blockLocations[index];
            _bufferPosition = truePosition % _dataBuffer.Length;

            long count = _dataBuffer.Length;
            if (index == _blockLocations.Length - 1)
            {
                count = (_length - _position) + _bufferPosition;
            }

            long offset = index == 0 ? _initialOffset : 0;
            count -= offset;

            if (_filestream.Read(_dataBuffer.Slice(offset, count)) != count)
            {
                throw new IOException("Buffer update error");
            }
        }

        private CONFileStream(FileStream stream, long length, long offset, ref FixedArray<byte> buffer, ref FixedArray<long> locations)
        {
            _filestream = stream;
            _length = length;
            _initialOffset = offset;
            _dataBuffer = buffer.TransferOwnership();
            _blockLocations = locations.TransferOwnership();
            _bufferIndex = -1;
        }

        public static long CalculateBlockLocation(long blockNum, int shift)
        {
            long blockAdjust = 0;
            if (blockNum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += ((blockNum / BLOCKS_PER_SECTION) + 1) << shift;
                if (blockNum >= NUM_BLOCKS_SQUARED)
                {
                    blockAdjust += ((blockNum / NUM_BLOCKS_SQUARED) + 1) << shift;
                }
            }
            return FIRSTBLOCK_OFFSET + (blockAdjust + blockNum) * BYTES_PER_BLOCK;
        }

        public static CONFileStream CreateStream(string path, CONFileListing listing)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            try
            {
                FixedArray<byte> dataBuffer;
                FixedArray<long> blockLocations;
                long initialOffset;

                long currentBlock = listing.BlockOffset;
                if (listing.IsContiguous())
                {
                    long blockOffset = currentBlock % BLOCKS_PER_SECTION;
                    initialOffset = blockOffset * BYTES_PER_BLOCK;

                    long totalBlocks = listing.BlockCount + blockOffset;
                    long numSections = totalBlocks / BLOCKS_PER_SECTION;
                    if (totalBlocks % BLOCKS_PER_SECTION > 0)
                    {
                        ++numSections;
                    }

                    using var contiguousLocations = FixedArray<long>.Alloc(numSections);
                    long blockMovement = BLOCKS_PER_SECTION - blockOffset;
                    long skipVal = BYTES_PER_BLOCK << listing.Shift;
                    long threshold = ((currentBlock / NUM_BLOCKS_SQUARED) + 1) * NUM_BLOCKS_SQUARED;
                    long location = CalculateBlockLocation(currentBlock, listing.Shift);

                    for (int i = 0; i < numSections; ++i)
                    {
                        contiguousLocations[i] = location;
                        if (i < numSections - 1)
                        {
                            currentBlock += blockMovement;

                            long seekCount = 1;
                            if (currentBlock == BLOCKS_PER_SECTION)
                            {
                                seekCount = 2;
                            }
                            else if (currentBlock == threshold)
                            {
                                seekCount = currentBlock == NUM_BLOCKS_SQUARED ? 3 : 2;
                                threshold += NUM_BLOCKS_SQUARED;
                            }

                            location += blockMovement * BYTES_PER_BLOCK + seekCount * skipVal;
                            blockMovement = BLOCKS_PER_SECTION;
                        }
                    }
                    blockLocations = contiguousLocations.TransferOwnership();
                    dataBuffer = FixedArray<byte>.Alloc(BYTES_PER_SECTION);
                }
                else
                {
                    initialOffset = 0;

                    using var splitLocations = FixedArray<long>.Alloc(listing.BlockCount);
                    using var hashBlock = FixedArray<byte>.Alloc(BYTES_PER_BLOCK);
                    var hashSpan = hashBlock.Span;
                    for (int i = 0; i < listing.BlockCount; ++i)
                    {
                        long location = splitLocations[i] = CalculateBlockLocation(currentBlock, listing.Shift);
                        if (i < listing.BlockCount - 1)
                        {
                            long blockOffset = currentBlock % BLOCKS_PER_SECTION;
                            long hashLocation = location - ((blockOffset + 1) * BYTES_PER_BLOCK);
                            stream.Position = hashLocation;

                            if (stream.Read(hashSpan) != BYTES_PER_BLOCK)
                            {
                                throw new IOException("Hashblock Read error");
                            }

                            long next_block_hash_index = blockOffset * BYTES_PER_HASH_ENTRY + NEXT_BLOCK_HASH_OFFSET;
                            currentBlock = (long) hashBlock[next_block_hash_index] << 16 |
                                           (long) hashBlock[next_block_hash_index + 1] << 8 |
                                                  hashBlock[next_block_hash_index + 2];
                        }
                    }
                    blockLocations = splitLocations.TransferOwnership();
                    dataBuffer = FixedArray<byte>.Alloc(BYTES_PER_BLOCK);
                }
                return new CONFileStream(stream, listing.Length, initialOffset, ref dataBuffer, ref blockLocations);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public static FixedArray<byte> LoadFile(string path, CONFileListing listing)
        {
            using var filestream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return LoadFile(filestream, listing);
        }

        public static FixedArray<byte> LoadFile(Stream stream, CONFileListing listing)
        {
            using var data = FixedArray<byte>.Alloc(listing.Length);
            
            long currentBlock = listing.BlockOffset;
            if (listing.IsContiguous())
            {
                long numBlocks = BLOCKS_PER_SECTION - (currentBlock % BLOCKS_PER_SECTION);
                long readSize = BYTES_PER_BLOCK * numBlocks;
                long remaining = listing.Length;
                while (remaining > 0)
                {
                    stream.Position = CalculateBlockLocation(currentBlock, listing.Shift);
                    if (readSize > remaining)
                    {
                        readSize = remaining;
                    }

                    if (stream.Read(data.Slice(listing.Length - remaining, readSize)) != readSize)
                    {
                        throw new Exception("Block read error in CON subfile - Continguous");
                    }

                    remaining -= readSize;
                    currentBlock += numBlocks;
                    numBlocks = BLOCKS_PER_SECTION;
                    readSize = BYTES_PER_SECTION;
                }
            }
            else
            {
                using var hashBlock = FixedArray<byte>.Alloc(BYTES_PER_BLOCK);
                var hashSpan = hashBlock.Span;
                unsafe
                {
                    byte* position = data.Ptr;
                    for (int i = 0; i < listing.BlockCount; ++i)
                    {
                        long blockLocation = CalculateBlockLocation(currentBlock, listing.Shift);
                        stream.Position = blockLocation;

                        long readCount = i + 1 < listing.BlockCount ? BYTES_PER_BLOCK : listing.Length % BYTES_PER_BLOCK;
                        if (stream.Read(new Span<byte>(position, (int)readCount)) != readCount)
                        {
                            throw new Exception("Block read error in CON subfile - Split");
                        }
                        position += readCount;

                        if (i + 1 < listing.BlockCount)
                        {
                            long blockOffset = currentBlock % BLOCKS_PER_SECTION;
                            long hashLocation = blockLocation - ((blockOffset + 1) * BYTES_PER_BLOCK);

                            if (hashLocation < 0)
                            {
                                throw new Exception("Ha");
                            }

                            stream.Position = hashLocation;
                            if (stream.Read(hashSpan) != BYTES_PER_BLOCK)
                            {
                                throw new Exception("Hashblock read error in CON subfile");
                            }

                            long next_block_hash_index = blockOffset * BYTES_PER_HASH_ENTRY + NEXT_BLOCK_HASH_OFFSET;
                            currentBlock = (long) hashBlock[next_block_hash_index] << 16 |
                                           (long) hashBlock[next_block_hash_index + 1] << 8 |
                                                  hashBlock[next_block_hash_index + 2];
                        }
                    }
                }
            }
            return data.TransferOwnership();
        }
    }
}
