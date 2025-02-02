using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;

namespace YARG.Core.IO
{
    public struct SngFileListing
    {
        public long Position;
        public long Length;
    }

    public class SngTracker : IDisposable
    {
        private int _count = 1;

        public Stream Stream = null!;
        public SngMask Mask;

        public SngTracker AddOwner()
        {
            lock (this)
            {
                if (_count == 0)
                {
                    throw new ObjectDisposedException("");
                }
                ++_count; 
            }
            return this;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_count == 0)
                {
                    throw new ObjectDisposedException("");
                }

                if (--_count == 0)
                {
                    Stream?.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// <see href="https://github.com/mdsitton/SngFileFormat">Documentation of SNG file type</see>
    /// </summary>
    public struct SngFile : IDisposable
    {
        private SngTracker _tracker;

        private uint _version;
        private IniModifierCollection _modifiers;
        private Dictionary<string, SngFileListing> _listings;

        public readonly uint Version => _version;
        public readonly IniModifierCollection Modifiers => _modifiers;
        public readonly Dictionary<string, SngFileListing> Listings => _listings;

        public readonly bool IsLoaded => _tracker != null;

        public readonly bool TryGetListing(string name, out SngFileListing listing)
        {
            return _listings.TryGetValue(name, out listing);
        }

        public readonly FixedArray<byte> LoadAllBytes(in SngFileListing listing)
        {
            FixedArray<byte> data;
            lock (_tracker.Stream)
            {
                _tracker.Stream.Position = listing.Position;
                data = FixedArray.Read(_tracker.Stream, listing.Length);
            }

            unsafe
            {
                SngFileStream.DecryptVectorized(data.Ptr, _tracker.Mask, data.Ptr + listing.Length);
            }
            return data;
        }

        public readonly SngFileStream CreateStream(string name, in SngFileListing listing)
        {
            return new SngFileStream(name, in listing, _tracker);
        }

        public readonly void Dispose()
        {
            _tracker.Dispose();
        }

        private static readonly byte[] SNGPKG = { (byte) 'S', (byte) 'N', (byte) 'G', (byte) 'P', (byte) 'K', (byte) 'G' };
        public static SngFile TryLoadFromFile(string filename, bool loadMetadata)
        {
            using var tracker = new SngTracker();
            var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (YARGSongFileStream.TryLoad(filestream, out var yargStream))
            {
                yargStream.Position = SNGPKG.Length;
                tracker.Stream = yargStream;
            }
            else
            {
                filestream.Position = 0;
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                if (filestream.Read(tag) < tag.Length || !tag.SequenceEqual(SNGPKG))
                {
                    return default;
                }
                tracker.Stream = filestream;
            }

            SngFile sng = new()
            {
                _version = tracker.Stream.Read<uint>(Endianness.Little)
            };

            tracker.Mask = SngMask.LoadMask(tracker.Stream);
            if (loadMetadata)
            {
                sng._modifiers = new IniModifierCollection();
                LoadMetadata(sng._modifiers, tracker.Stream);
            }
            else
            {
                long length = tracker.Stream.Read<long>(Endianness.Little);
                tracker.Stream.Position += length;
            }

            sng._listings = new Dictionary<string, SngFileListing>();
            LoadListings(sng._listings, tracker.Stream);
            // Allow the SngFile instance to own the tracker after the `using` call
            sng._tracker = tracker.AddOwner();
            return sng;
        }

        public static bool ValidateMatch(string filename, uint versionToMatch)
        {
            using var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            Stream basestream;
            if (YARGSongFileStream.TryLoad(filestream, out var yargStream))
            {
                yargStream.Position = SNGPKG.Length;
                basestream = yargStream;
            }
            else
            {
                filestream.Position = 0;
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                if (filestream.Read(tag) < tag.Length || !tag.SequenceEqual(SNGPKG))
                {
                    return false;
                }
                basestream = filestream;
            }

            using (basestream)
            {
                return basestream.Read<uint>(Endianness.Little) == versionToMatch;
            }
        }

        private static void LoadMetadata(IniModifierCollection modifiers, Stream stream)
        {
            long length = stream.Read<long>(Endianness.Little) - sizeof(ulong);
            ulong numPairs = stream.Read<ulong>(Endianness.Little);

            using var bytes = FixedArray.Read(stream, length);
            YARGTextContainer<byte> container;
            unsafe
            {
                container = new YARGTextContainer<byte>(in bytes, null!);
            }

            for (ulong i = 0; i < numPairs; ++i)
            {
                int strLength = GetLength(ref container);
                string key;
                unsafe
                {
                    key = Encoding.UTF8.GetString(container.PositionPointer, strLength);
                }
                container.Position += strLength;

                strLength = GetLength(ref container);
                long next = container.Position + strLength;
                if (SongIniHandler.SONG_INI_OUTLINES.TryGetValue(key, out var outline))
                {
                    modifiers.AddSng(ref container, strLength, outline);
                }
                container.Position = next;
            }
        }

        private static void LoadListings(Dictionary<string, SngFileListing> listings, Stream stream)
        {
            long length = stream.Read<long>(Endianness.Little) - sizeof(ulong);
            ulong numListings = stream.Read<ulong>(Endianness.Little);

            using var bytes = FixedArray.Read(stream, length);
            listings.EnsureCapacity((int)numListings);

            ulong listingIndex = 0;
            long buffPosition = 0;
            while (listingIndex < numListings)
            {
                if (buffPosition == bytes.Length)
                {
                    throw new EndOfStreamException();
                }

                int strlen = bytes[buffPosition++];
                if (buffPosition + strlen + 2 * sizeof(long) > bytes.Length)
                {
                    throw new EndOfStreamException();
                }

                string filename;
                SngFileListing listing;
                unsafe
                {
                    filename = Encoding.UTF8.GetString(bytes.Ptr + buffPosition, strlen);
                    buffPosition += strlen;
                    listing.Length = *(long*)&bytes.Ptr[buffPosition];
                    buffPosition += sizeof(long);
                    listing.Position = *(long*) &bytes.Ptr[buffPosition];
                    buffPosition += sizeof(long);
                }
                listings.Add(filename.ToLower(), listing);
                ++listingIndex;
            }
        }

        private static int GetLength(ref YARGTextContainer<byte> container)
        {
            if (container.Position + sizeof(int) > container.Length)
            {
                throw new EndOfStreamException();
            }

            int length;
            unsafe
            {
                length = *(int*) container.PositionPointer;
            }

            container.Position += sizeof(int);
            if (container.Position + length > container.Length)
            {
                throw new EndOfStreamException();
            }
            return length;
        }
    }
}
