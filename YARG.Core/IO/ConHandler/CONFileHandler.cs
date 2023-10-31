using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.IO
{
    public class CONFile : IDisposable
    {
        public readonly string Name;
        public readonly SharedCONStream Stream;
        public readonly List<CONFileListing> Listings = new();

        public CONFile(string filename)
        {
            Name = filename;
            Stream = new SharedCONStream(filename);
        }

        public void Dispose()
        {
            Listings.Clear();
            Stream.Dispose();
        }
    }


    public static class CONFileHandler
    {
        private static readonly FourCC CON_TAG = new('C', 'O', 'N', ' ');
        private static readonly FourCC LIVE_TAG = new('L', 'I', 'V', 'E');
        private static readonly FourCC PIRS_TAG = new('P', 'I', 'R', 'S');

        private const int METADATA_POSITION = 0x340;
        private const int FILETABLEBLOCKCOUNT_POSITION = 0x37C;
        private const int FILETABLEFIRSTBLOCK_POSITION = 0x37E;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;

        private const int BYTES_PER_BLOCK = 0x1000;
        private const int SIZEOF_FILELISTING = 0x40;
        
        public static CONFile? TryLoadCONFile(string filename)
        {
            Span<byte> int32Buffer = stackalloc byte[BYTES_32BIT];
            using FileStream stream = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.Read(int32Buffer) != BYTES_32BIT)
                return null;

            var tag = new FourCC(int32Buffer);
            if (tag != CON_TAG && tag != LIVE_TAG && tag != PIRS_TAG)
                return null;

            stream.Seek(METADATA_POSITION, SeekOrigin.Begin);
            if (stream.Read(int32Buffer) != BYTES_32BIT)
                return null;

            byte shift = 0;
            int entryID = int32Buffer[0] << 24 | int32Buffer[1] << 16 | int32Buffer[2] << 8 | int32Buffer[3];

            // Docs: "If bit 12, 13 and 15 of the Entry ID are on, there are 2 hash tables every 0xAA (170) blocks"
            if ((entryID + 0xFFF & 0xF000) >> 0xC != 0xB)
                shift = 1;

            stream.Seek(FILETABLEBLOCKCOUNT_POSITION, SeekOrigin.Begin);
            if (stream.Read(int32Buffer[..BYTES_16BIT]) != BYTES_16BIT)
                return null;

            int length = BYTES_PER_BLOCK * (int32Buffer[0] << 8 | int32Buffer[1]);

            stream.Seek(FILETABLEFIRSTBLOCK_POSITION, SeekOrigin.Begin);
            if (stream.Read(int32Buffer[..BYTES_24BIT]) != BYTES_24BIT)
                return null;

            int firstBlock = int32Buffer[0] << 16 | int32Buffer[1] << 8 | int32Buffer[2];

            try
            {
                AbridgedFileInfo fileInfo = new(filename);
                CONFile conFile = new(filename);

                using var conStream = new CONFileStream(stream, true, length, firstBlock, shift);
                Span<byte> listingBuffer = stackalloc byte[SIZEOF_FILELISTING];
                for (int i = 0; i < length; i += SIZEOF_FILELISTING)
                {
                    conStream.Read(listingBuffer);
                    if (listingBuffer[0] == 0)
                        break;

                    CONFileListing listing = new(fileInfo, shift, listingBuffer);
                    if (listing.pathIndex != -1)
                        listing.SetParentDirectory(conFile.Listings[listing.pathIndex].Filename);
                    conFile.Listings.Add(listing);
                }
                return conFile;
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while parsing {filename} (usually when the file doesn't follow spec)");
                return null;
            }
        }

        public static CONFileListing? TryGetListing(List<CONFileListing> files, string filename)
        {
            for (int i = 0; i < files.Count; ++i)
            {
                var listing = files[i];
                if (filename == listing.Filename)
                    return listing;
            }
            return null;
        }
    }
}
