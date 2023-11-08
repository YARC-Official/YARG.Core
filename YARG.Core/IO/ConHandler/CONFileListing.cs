using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class CONFileListing
    {
        public enum CONFileListingFlag : byte
        {
            Contiguous = 0x40,
            Directory = 0x80,
        }

        public readonly AbridgedFileInfo ConFile;
        private readonly int shift;

        public string Filename { get; private set; } = string.Empty;
        public readonly CONFileListingFlag flags;
        public readonly int numBlocks;
        public readonly int firstBlock;
        public readonly short pathIndex;
        public readonly int size;
        public readonly DateTime lastWrite;

        public CONFileListing(AbridgedFileInfo conFile, int shift, ReadOnlySpan<byte> data)
        {
            ConFile = conFile;
            this.shift = shift;

            Filename = Encoding.UTF8.GetString(data[..0x28]).TrimEnd('\0');
            flags = (CONFileListingFlag) data[0x28];

            numBlocks = data[0x29] << 16 | data[0x2A] << 8 | data[0x2B];
            firstBlock = data[0x31] << 16 | data[0x30] << 8 | data[0x2F];
            pathIndex = (short) (data[0x32] << 8 | data[0x33]);
            size = data[0x34] << 24 | data[0x35] << 16 | data[0x36] << 8 | data[0x37];
            lastWrite = FatTimeDT(data[0x3B] << 24 | data[0x3A] << 16 | data[0x39] << 8 | data[0x38]);
        }

        public void SetParentDirectory(string parentDirectory)
        {
            Filename = parentDirectory + "/" + Filename;
        }

        public override string ToString() => $"STFS File Listing: {Filename}";
        public bool IsDirectory() { return (flags & CONFileListingFlag.Directory) > 0; }
        public bool IsContiguous() { return (flags & CONFileListingFlag.Contiguous) > 0; }
        public bool IsStillValid() { return ConFile.IsStillValid(); }

        public CONFileStream CreateStream()
        {
            Debug.Assert(!IsDirectory(), "Directory listing cannot be loaded as a file");
            return new CONFileStream(ConFile.FullName, IsContiguous(), size, firstBlock, shift);
        }

        // This overload should only be called during scanning
        public byte[] LoadAllBytes(CONFile file)
        {
            lock (file.Lock)
                return CONFileStream.LoadFile(file.Stream, IsContiguous(), size, firstBlock, shift);
        }

        public byte[] LoadAllBytes()
        {
            return CONFileStream.LoadFile(ConFile.FullName, IsContiguous(), size, firstBlock, shift);
        }

        public static int GetMoggVersion(CONFileListing listing, CONFile file)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
            byte[] buffer;
            lock (file.Lock)
                buffer = CONFileStream.LoadFile(file.Stream, listing.IsContiguous(), 4, listing.firstBlock, listing.shift);
            return BitConverter.ToInt32(buffer);
        }

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
}
