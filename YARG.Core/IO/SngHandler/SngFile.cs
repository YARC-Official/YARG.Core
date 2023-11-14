using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;

namespace YARG.Core.IO
{
    public class SngFile : IDisposable, IEnumerable<KeyValuePair<string, SngFileListing>>
    {
        public readonly uint Version;
        public readonly SngMask Mask;
        public readonly IniSection Metadata;
        private readonly Dictionary<string, SngFileListing> listings;

        private SngFile(uint version, byte[] mask, IniSection metadata, Dictionary<string, SngFileListing> listings)
        {
            this.Version = version;
            Mask = new SngMask(mask);
            Metadata = metadata;
            this.listings = listings;
        }

        public SngFileListing this[string key] => listings[key];
        public bool ContainsKey(string key) => listings.ContainsKey(key);

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

        public static SngFile? TryLoadFile(string filename)
        {
            using var stream = InitStream_Internal(filename);
            if (stream == null)
                return null;

            {
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                if (stream.Read(tag) != tag.Length)
                    return null;

                if (!tag.SequenceEqual(SNGPKG))
                    return null;
            }

            try
            {
                uint version = stream.ReadUInt32LE();
                var xorMask = stream.ReadBytes(XORMASK_SIZE);
                var metadata = ReadMetadata(stream);
                var listings = ReadListings(stream);
                return new SngFile(version, xorMask, metadata, listings);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error loading {filename}");
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
                YargTrace.LogException(ex, $"Error loading {filename}");
                return null;
            }
        }

        private static IniSection ReadMetadata(FileStream stream)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            ulong length = stream.ReadUInt64LE() - sizeof(ulong);
            ulong numPairs = stream.ReadUInt64LE();

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
                    text.Position += size;
            }
            return new IniSection(modifiers);
        }

        private static Dictionary<string, SngFileListing> ReadListings(FileStream stream)
        {
            ulong length = stream.ReadUInt64LE() - sizeof(ulong);
            ulong numListings = stream.ReadUInt64LE();

            Dictionary<string, SngFileListing> listings = new((int)numListings);

            YARGBinaryReader reader = new(stream, (int)length);
            for (ulong i = 0; i < numListings; i++)
            {
                var strLen = reader.ReadByte();
                string filename = Encoding.UTF8.GetString(reader.ReadBytes(strLen)).ToLower();
                listings.Add(filename, new SngFileListing(reader));
            }
            return listings;
        }
    }
}
