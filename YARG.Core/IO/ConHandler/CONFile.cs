using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class CONFile
    {
        public readonly string filename;
        public readonly byte shift = 0;

        private readonly List<CONFileListing> files = new();

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
                return new(stream, shift, firstBlock, length);
            }
            catch
            {
                return null;
            }
        }

        private const int SIZEOF_FILELISTING = 0x40;
        private CONFile(FileStream stream, byte shift, int firstBlock, int length)
        {
            using var conStream = new CONFileStream(stream, true, length, firstBlock, shift);
            Span<byte> buffer = stackalloc byte[SIZEOF_FILELISTING];
            for (int i = 0; i < length; i += SIZEOF_FILELISTING)
            {
                conStream.Read(buffer);
                if (buffer[0] == 0)
                    break;

                CONFileListing listing = new(buffer);
                if (listing.pathIndex != -1)
                    listing.SetParentDirectory(files[listing.pathIndex].Filename);
                files.Add(listing);
            }

            filename = stream.Name;
            this.shift = shift;
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
            using var conStream = CreateStream(listing);
            return conStream.ReadInt32LE();
        }

        public CONFileStream CreateStream(CONFileListing listing)
        {
            Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return new CONFileStream(fs, listing, shift);
        }

        public byte[] LoadSubFile(CONFileListing listing)
        {
            using var conStream = CreateStream(listing);
            return conStream.ReadBytes(listing.size);
        }
    }
}
