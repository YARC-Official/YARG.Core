using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace YARG.Core.IO
{
    public enum CONFileListingFlag : byte
    {
        Contiguous = 0x40,
        Directory = 0x80,
    }

    public sealed class CONFileListing
    {
        public string Filename { get; private set; } = string.Empty;
        public readonly CONFileListingFlag flags;
        public readonly int numBlocks;
        public readonly int firstBlock;
        public readonly short pathIndex;
        public readonly int size;
        public readonly DateTime lastWrite;

        public CONFileListing(ReadOnlySpan<byte> data)
        {
            Filename = Encoding.UTF8.GetString(data[..0x28]).TrimEnd('\0');
            flags = (CONFileListingFlag)data[0x28];

            numBlocks =           data[0x29] << 16 | data[0x2A] << 8 | data[0x2B];
            firstBlock =          data[0x31] << 16 | data[0x30] << 8 | data[0x2F];
            pathIndex =  (short) (data[0x32] << 8  | data[0x33]);
            size =                data[0x34] << 24 | data[0x35] << 16 | data[0x36] << 8 | data[0x37];
            lastWrite = FatTimeDT(data[0x3B] << 24 | data[0x3A] << 16 | data[0x39] << 8 | data[0x38]);
        }

        public void SetParentDirectory(string parentDirectory)
        {
            Filename = parentDirectory + "/" + Filename;
        }

        public CONFileListing() { }

        public override string ToString() => $"STFS File Listing: {Filename}";
        public bool IsDirectory() { return (flags & CONFileListingFlag.Directory) > 0; }
        public bool IsContiguous() { return (flags & CONFileListingFlag.Contiguous) > 0; }

        public static DateTime FatTimeDT(int fatTime)
        {
            int time = fatTime & 0xFFFF;
            int date = fatTime >> 16;
            if (date == 0 && time == 0)
                return DateTime.Now;

            int seconds = time & 0b11111;
            int minutes = (time >> 5) & 0b111111;
            int hour = (time >> 11) & 0b11111;

            int day = date & 0b11111;
            int month = (date >> 5) & 0b1111;
            int year = (date >> 9) & 0b1111111;

            if (day == 0)
                day = 1;

            if (month == 0)
                month = 1;

            return new DateTime(year + 1980, month, day, hour, minutes, 2 * seconds);
        }
    }

    public sealed class CONFile
    {
        public readonly string filename;
        private readonly FileStream stream;
        private readonly byte shift = 0;
        private readonly List<CONFileListing> files = new();
        private readonly object fileLock = new();

        private const int METADATA_POSITION = 0x340;
        private const int FILETABLEBLOCKCOUNT_POSITION = 0x37C;
        private const int FILETABLEFIRSTBLOCK_POSITION = 0x37E;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;

        private const int BYTES_PER_BLOCK = 0x1000;

        public static CONFile? LoadCON(string filename)
        {
            byte[] buffer = new byte[BYTES_32BIT];
            using FileStream stream = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.Read(buffer) != BYTES_32BIT)
                return null;

            string tag = Encoding.Default.GetString(buffer, 0, buffer.Length);
            if (tag != "CON " && tag != "LIVE" && tag != "PIRS")
                return null;

            stream.Seek(METADATA_POSITION, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, BYTES_32BIT) != BYTES_32BIT)
                return null;

            byte shift = 0;
            int entryID = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];

            // Docs: "If bit 12, 13 and 15 of the Entry ID are on, there are 2 hash tables every 0xAA (170) blocks"
            if ((entryID + 0xFFF & 0xF000) >> 0xC != 0xB)
                shift = 1;

            stream.Seek(FILETABLEBLOCKCOUNT_POSITION, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, BYTES_16BIT) != BYTES_16BIT)
                return null;

            int length = BYTES_PER_BLOCK * (buffer[0] << 8 | buffer[1]);

            stream.Seek(FILETABLEFIRSTBLOCK_POSITION, SeekOrigin.Begin);
            if (stream.Read(buffer, 0, BYTES_24BIT) != BYTES_24BIT)
                return null;

            int firstBlock = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
            try
            {

                return new(filename, shift, firstBlock, length);
            }
            catch
            {
                return null;
            }
        }

        private CONFile(string filename, byte shift, int firstBlock, int length)
        {
            this.filename = filename;
            stream = new(filename, FileMode.Open, FileAccess.Read);
            this.shift = shift;
            ParseFileList(firstBlock, length);
        }

        ~CONFile()
        {
            stream.Dispose();
        }

        private const int SIZEOF_FILELISTING = 0x40;
        private void ParseFileList(int firstBlock, int length)
        {
            var fileListingBuffer = ReadContiguousBlocks(firstBlock, length);
            for (int i = 0; i < length; i += SIZEOF_FILELISTING)
            {
                CONFileListing listing = new(new(fileListingBuffer, i, SIZEOF_FILELISTING));
                // Empty node == End of list
                if (listing.Filename.Length == 0)
                    break;

                if (listing.pathIndex != -1)
                    listing.SetParentDirectory(files[listing.pathIndex].Filename);
                files.Add(listing);
            }
        }

        public CONFileListing this[int index] { get { return files[index]; } }
        public CONFileListing? TryGetListing(string filename)
        {
            for (int i = 0; i < files.Count; ++i)
            {
                var listing = files[i];
                if (filename == listing.Filename)
                    return listing;
            }
            return null;
        }

        private const int FIRSTBLOCK_OFFSET = 0xC000;
        public int GetMoggVersion(CONFileListing listing)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");

            long blockLocation = FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(listing.firstBlock) * BYTES_PER_BLOCK;
            byte[] version = new byte[BYTES_32BIT];
            lock (fileLock)
            {
                stream.Seek(blockLocation, SeekOrigin.Begin);
                if (stream.Read(version, 0, BYTES_32BIT) != BYTES_32BIT)
                    throw new Exception("Seek error in CON-like subfile for Mogg");
            }
            return BitConverter.ToInt32(version);
        }

        public byte[] LoadSubFile(CONFileListing listing)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
            try
            {
                if (listing.IsContiguous())
                    return ReadContiguousBlocks(listing.firstBlock, listing.size);
                else
                    return ReadSplitBlocks(listing.firstBlock, listing.size);
            }
            catch (Exception e)
            {
                throw new Exception(filename + ": " + e.Message);
            }
        }

        private const int BLOCKS_PER_SECTION = 170;
        private const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        private const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        private byte[] ReadContiguousBlocks(int blockNum, int fileSize)
        {
            byte[] data = new byte[fileSize];
            long skipVal = BYTES_PER_BLOCK << shift;
            int threshold = blockNum - blockNum % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
            int numBlocks = BLOCKS_PER_SECTION - blockNum % BLOCKS_PER_SECTION;
            int readSize = BYTES_PER_BLOCK * numBlocks;
            int offset = 0;

            lock (fileLock)
            {
                stream.Seek(FIRSTBLOCK_OFFSET + (long) CalculateBlockNum(blockNum) * BYTES_PER_BLOCK, SeekOrigin.Begin);
                while (true)
                {
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    if (stream.Read(data, offset, readSize) != readSize)
                        throw new Exception("Read error in CON-like subfile - Type: Contiguous");

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

                    stream.Seek(seekCount * skipVal, SeekOrigin.Current);
                }
            }
            return data;
        }

        private const int HASHBLOCK_OFFSET = 4075;
        private const int DIST_PER_HASH = 4072;
        private byte[] ReadSplitBlocks(int blockNum, int fileSize)
        {
            byte[] data = new byte[fileSize];
            byte[] buffer = new byte[3];

            int offset = 0;
            while (true)
            {
                int block = CalculateBlockNum(blockNum);
                long blockLocation = FIRSTBLOCK_OFFSET + (long) block * BYTES_PER_BLOCK;
                int readSize = BYTES_PER_BLOCK;
                if (readSize > fileSize - offset)
                    readSize = fileSize - offset;

                lock (fileLock)
                {
                    stream.Seek(blockLocation, SeekOrigin.Begin);
                    if (stream.Read(data, offset, readSize) != readSize)
                        throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
                }

                offset += readSize;
                if (offset == fileSize)
                    break;

                long hashlocation = blockLocation - ((long) (blockNum % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                lock (fileLock)
                {
                    stream.Seek(hashlocation, SeekOrigin.Begin);
                    if (stream.Read(buffer, 0, BYTES_24BIT) != BYTES_24BIT)
                        throw new Exception("Post-Read error in CON-like subfile - Type: Split");
                }

                blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
            }
            return data;
        }

        private int CalculateBlockNum(int blocknum)
        {
            int blockAdjust = 0;
            if (blocknum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blocknum / BLOCKS_PER_SECTION + 1 << shift;
                if (blocknum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blocknum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return blockAdjust + blocknum;
        }
    }
}
