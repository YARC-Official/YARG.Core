using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public class CONFile
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

        private readonly List<string> _filenames;
        private readonly Dictionary<string, CONFileListing> _listings;

        private CONFile(List<string> filenames, Dictionary<string, CONFileListing> listings)
        {
            _filenames = filenames;
            _listings = listings;
        }

        public string GetFilename(int index)
        {
            return _filenames[index];
        }

        public bool TryGetListing(string name, out CONFileListing listing)
        {
            return _listings.TryGetValue(name, out listing);
        }

        public static CONFile? TryParseListings(AbridgedFileInfo info)
        {
            using var stream = InitStream_Internal(info.FullName);
            if (stream == null)
                return null;

            Span<byte> int32Buffer = stackalloc byte[BYTES_32BIT];
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

            int length = BYTES_PER_BLOCK * (int32Buffer[0] | int32Buffer[1] << 8);

            stream.Seek(FILETABLEFIRSTBLOCK_POSITION, SeekOrigin.Begin);
            if (stream.Read(int32Buffer[..BYTES_24BIT]) != BYTES_24BIT)
                return null;

            int firstBlock = int32Buffer[0] << 16 | int32Buffer[1] << 8 | int32Buffer[2];

            try
            {
                var filenames = new List<string>();
                var listings = new Dictionary<string, CONFileListing>();

                using var conStream = new CONFileStream(stream, true, length, firstBlock, shift);
                Span<byte> listingBuffer = stackalloc byte[SIZEOF_FILELISTING];
                while (conStream.Read(listingBuffer) == SIZEOF_FILELISTING && listingBuffer[0] != 0)
                {
                    short pathIndex = (short) (listingBuffer[0x32] << 8 | listingBuffer[0x33]);
                    if (pathIndex >= filenames.Count)
                    {
                        YargLogger.LogFormatError("Error while parsing {0} - Filelisting blocks constructed out of spec", info.FullName);
                        return null;
                    }

                    string filename = pathIndex >= 0 ? filenames[pathIndex] + "/" : string.Empty;
                    filename += Encoding.UTF8.GetString(listingBuffer[..0x28]).TrimEnd('\0');
                    filenames.Add(filename);

                    var listing = new CONFileListing(info, filename, pathIndex, shift, listingBuffer);
                    listings.Add(filename, listing);
                }
                return new CONFile(filenames, listings);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while parsing {info.FullName}");
                return null;
            }
        }

        private static FileStream? InitStream_Internal(string filename)
        {
            try
            {
                return new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error loading {filename}");
                return null;
            }
        }
    }
}
