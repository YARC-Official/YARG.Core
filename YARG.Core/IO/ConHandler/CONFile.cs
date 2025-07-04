using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public static class CONFile
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

        public static bool FindListing(this List<CONFileListing> listings, string name, out CONFileListing listing)
        {
            foreach (var file in listings)
            {
                if (file.Name == name)
                {
                    listing = file;
                    return true;
                }
            }
            listing = null!;
            return false;
        }

        public static unsafe List<CONFileListing>? TryParseListings(string filename, FileStream filestream)
        {
            if (filestream.Length <= CONFileStream.FIRSTBLOCK_OFFSET)
            {
                return null;
            }

            var tag = new FourCC(filestream);
            if (tag != CON_TAG && tag != LIVE_TAG && tag != PIRS_TAG)
            {
                return null;
            }

            Span<byte> buffer = stackalloc byte[BYTES_32BIT];
            filestream.Position = METADATA_POSITION;
            if (filestream.Read(buffer) != BYTES_32BIT)
            {
                return null;
            }

            byte shift = 0;
            int entryID = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];

            // Docs: "If bit 12, 13 and 15 of the Entry ID are on, there are 2 hash tables every 0xAA (170) blocks"
            if ((entryID + 0xFFF & 0xF000) >> 0xC != 0xB)
            {
                shift = 1;
            }

            filestream.Position = FILETABLEBLOCKCOUNT_POSITION;
            if (filestream.Read(buffer[..BYTES_16BIT]) != BYTES_16BIT)
            {
                return null;
            }

            int length = BYTES_PER_BLOCK * (buffer[0] | buffer[1] << 8);

            filestream.Position = FILETABLEFIRSTBLOCK_POSITION;
            if (filestream.Read(buffer[..BYTES_24BIT]) != BYTES_24BIT)
            {
                return null;
            }

            try
            {
                filestream.Position = CONFileStream.CalculateBlockLocation(buffer[0] << 16 | buffer[1] << 8 | buffer[2], shift);
                using var listingBuffer = FixedArray.Read(filestream, length);

                var listings = new List<CONFileListing>();
                unsafe
                {
                    var endPtr = listingBuffer.Ptr + length;
                    for (var currPtr = listingBuffer.Ptr; currPtr + SIZEOF_FILELISTING <= endPtr && currPtr[0] != 0; currPtr += SIZEOF_FILELISTING)
                    {
                        short pathIndex = (short) (currPtr[0x32] << 8 | currPtr[0x33]);
                        string root = string.Empty;
                        if (pathIndex >= 0)
                        {
                            if (pathIndex >= listings.Count)
                            {
                                YargLogger.LogFormatError("Error while parsing {0} - Filelisting blocks constructed out of spec", filename);
                                return null;
                            }
                            root = listings[pathIndex].Name + "/";
                        }

                        var listing = new CONFileListing()
                        {
                            Name = root + Encoding.UTF8.GetString(currPtr, currPtr[0x28] & 0x3F),
                            Flags = (CONFileListing.Flag) (currPtr[0x28] & 0xC0),
                            BlockCount =  currPtr[0x2B] << 16 | currPtr[0x2A] <<  8 | currPtr[0x29],
                            BlockOffset = currPtr[0x31] << 16 | currPtr[0x30] <<  8 | currPtr[0x2F],
                            PathIndex = pathIndex,
                            Length =      currPtr[0x34] << 24 | currPtr[0x35] << 16 | currPtr[0x36] << 8 | currPtr[0x37],
                            LastWrite = FatTimeDT(currPtr[0x3B] << 24 | currPtr[0x3A] << 16 | currPtr[0x39] << 8 | currPtr[0x38]),
                            Shift = shift,
                        };
                        listings.Add(listing);
                    }
                }
                return listings;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while parsing {filename}");
                return null;
            }
        }

        private static DateTime FatTimeDT(int fatTime)
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
