﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Disposables;

namespace YARG.Core.IO
{
    public sealed class CONFileListing
    {
        [Flags]
        public enum CONFileListingFlag : byte
        {
            Contiguous = 0x40,
            Directory = 0x80,
        }

        private readonly int _shift;

        public readonly AbridgedFileInfo ConFile;
        public readonly string Filename;
        public readonly CONFileListingFlag Flags;
        public readonly int NumBlocks;
        public readonly int FirstBlock;
        public readonly short PathIndex;
        public readonly int Size;
        public readonly DateTime LastWrite;

        public CONFileListing(AbridgedFileInfo conFile, string name, short pathIndex, int shift, ReadOnlySpan<byte> data)
        {
            _shift = shift;

            ConFile = conFile;
            Filename = name;
            PathIndex = pathIndex;

            Flags = (CONFileListingFlag) data[0x28];
            NumBlocks = data[0x2B] << 16 | data[0x2A] << 8 | data[0x29];
            FirstBlock = data[0x31] << 16 | data[0x30] << 8 | data[0x2F];
            Size = data[0x34] << 24 | data[0x35] << 16 | data[0x36] << 8 | data[0x37];
            LastWrite = FatTimeDT(data[0x3B] << 24 | data[0x3A] << 16 | data[0x39] << 8 | data[0x38]);
        }

        public override string ToString() => $"STFS File Listing: {Filename}";
        public bool IsDirectory() { return (Flags & CONFileListingFlag.Directory) > 0; }
        public bool IsContiguous() { return (Flags & CONFileListingFlag.Contiguous) > 0; }
        public bool IsStillValid(in DateTime listingLastWrite) { return listingLastWrite == LastWrite && ConFile.IsStillValid(); }

        public CONFileStream CreateStream()
        {
            Debug.Assert(!IsDirectory(), "Directory listing cannot be loaded as a file");
            return new CONFileStream(ConFile.FullName, IsContiguous(), Size, FirstBlock, _shift);
        }

        // This overload should only be called during scanning
        public AllocatedArray<byte> LoadAllBytes(Stream stream)
        {
            lock (stream)
                return CONFileStream.LoadFile(stream, IsContiguous(), Size, FirstBlock, _shift);
        }

        public AllocatedArray<byte> LoadAllBytes()
        {
            return CONFileStream.LoadFile(ConFile.FullName, IsContiguous(), Size, FirstBlock, _shift);
        }

        public static int GetMoggVersion(CONFileListing listing, Stream stream)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
            long location = CONFileStream.CalculateBlockLocation(listing.FirstBlock, listing._shift);
            lock (stream)
            {
                stream.Seek(location, SeekOrigin.Begin);
                return stream.Read<int>(Endianness.Little);
            }
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