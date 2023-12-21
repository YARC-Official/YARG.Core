using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;

namespace YARG.Core.IO
{
    /// <summary>
    /// <see href="https://github.com/mdsitton/SngFileFormat">Documentation of SNG file type</see>
    /// </summary>
    public class SngFile : IDisposable, IEnumerable<KeyValuePair<string, SngFileListing>>
    {
        public readonly uint Version;
        public readonly SngMask Mask;
        public readonly IniSection Metadata;

        /// <summary>
        /// This is <b>not</b> part of the SNG spec.
        /// Indicates whether or not the <see cref="YARGSongFileStream"/> should be used.
        /// </summary>
        public readonly bool IsYARGSong;

        private readonly Dictionary<string, SngFileListing> listings;

        private SngFile(uint version, byte[] mask, IniSection metadata, bool isYARGSong,
            Dictionary<string, SngFileListing> listings)
        {
            Version = version;
            Mask = new SngMask(mask);
            Metadata = metadata;

            IsYARGSong = isYARGSong;

            this.listings = listings;
        }

        public SngFileListing this[string key] => listings[key];
        public bool ContainsKey(string key) => listings.ContainsKey(key);
        public bool TryGetValue(string key, out SngFileListing listing) => listings.TryGetValue(key, out listing);

        IEnumerator<KeyValuePair<string, SngFileListing>> IEnumerable<KeyValuePair<string, SngFileListing>>.GetEnumerator()
        {
            return listings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return listings.GetEnumerator();
        }

        public void Dispose()
        {
            Mask.Dispose();
        }


        private const int XORMASK_SIZE = 16;
        private const int BYTES_64BIT = 8;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;
        private static readonly byte[] SNGPKG = { (byte)'S', (byte) 'N', (byte) 'G', (byte)'P', (byte)'K', (byte)'G' };

        public static SngFile? TryLoadFromFile(string path, bool isYARGSong)
        {
            try
            {
                return LoadFromFile(path, isYARGSong);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error loading {path}.");
                return null;
            }
        }

        private static SngFile LoadFromFile(string path, bool isYARGSong)
        {
            using Stream stream = isYARGSong
                ? new YARGSongFileStream(path)
                : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!isYARGSong)
            {
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                if (stream.Read(tag) != tag.Length || !tag.SequenceEqual(SNGPKG))
                    throw new InvalidOperationException("`.sng` file signature does not match!");
            }
            else
            {
                // YARG SNGs replace the signature, so just skip it.
                // File signature will be checked when decrypting
                stream.Seek(SNGPKG.Length, SeekOrigin.Current);
            }

            uint version = stream.Read<uint>(Endianness.Little);
            var xorMask = stream.ReadBytes(XORMASK_SIZE);
            var metadata = ReadMetadata(stream);
            var listings = ReadListings(stream, isYARGSong);
            return new SngFile(version, xorMask, metadata, isYARGSong, listings);
        }

        private static IniSection ReadMetadata(Stream stream)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            ulong length = stream.Read<ulong>(Endianness.Little) - sizeof(ulong);
            ulong numPairs = stream.Read<ulong>(Endianness.Little);

            var validNodes = SongIniHandler.SONG_INI_DICTIONARY["[song]"];
            YARGTextContainer<byte> text = new(stream.ReadBytes((int)length), 0);
            for (ulong i = 0; i < numPairs; i++)
            {
                int size = BitConverter.ToInt32(text.Data, text.Position);
                text.Position += sizeof(int);

                var key = Encoding.UTF8.GetString(text.ExtractSpan(size));
                size = BitConverter.ToInt32(text.Data, text.Position);
                text.Position += sizeof(int);

                if (validNodes.TryGetValue(key, out var node))
                {
                    text.Next = text.Position + size;
                    var mod = node.CreateSngModifier(text);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                    text.Position = text.Next;
                }
                else
                {
                    text.Position += size;
                }
            }
            return new IniSection(modifiers);
        }

        private static Dictionary<string, SngFileListing> ReadListings(Stream stream, bool isYARGSong)
        {
            ulong length = stream.Read<ulong>(Endianness.Little) - sizeof(ulong);
            ulong numListings = stream.Read<ulong>(Endianness.Little);

            Dictionary<string, SngFileListing> listings = new((int)numListings);

            YARGBinaryReader reader = new(stream, (int)length);
            for (ulong i = 0; i < numListings; i++)
            {
                var strLen = reader.ReadByte();
                string filename = Encoding.UTF8.GetString(reader.ReadBytes(strLen));
                int idx = filename.LastIndexOf('/');
                if (idx != -1)
                    filename = filename[idx..];
                listings.Add(filename.ToLower(), new SngFileListing(reader, isYARGSong));
            }
            return listings;
        }
    }
}