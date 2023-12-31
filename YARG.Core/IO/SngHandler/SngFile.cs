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

        private readonly string _filename;
        private readonly Dictionary<string, SngFileListing> _listings;
        private readonly int[]? _values;

        private SngFile(uint version, byte[] mask, IniSection metadata, string filename, Dictionary<string, SngFileListing> listings, int[]? values)
        {
            Version = version;
            Mask = new SngMask(mask);
            Metadata = metadata;

            _filename = filename;
            _listings = listings;
            _values = values;
        }

        public byte[] LoadAllBytes(SngFileListing listing)
        {
            var stream = LoadFileStream();
            return SngFileStream.LoadFile(stream, Mask.Clone(), listing.Length, listing.Position);
        }

        public SngFileStream CreateStream(SngFileListing listing)
        {
            var stream = LoadFileStream();
            return new SngFileStream(stream, Mask.Clone(), listing.Length, listing.Position);
        }

        public SngFileListing this[string key] => _listings[key];
        public bool ContainsKey(string key) => _listings.ContainsKey(key);
        public bool TryGetValue(string key, out SngFileListing listing) => _listings.TryGetValue(key, out listing);

        IEnumerator<KeyValuePair<string, SngFileListing>> IEnumerable<KeyValuePair<string, SngFileListing>>.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }

        public void Dispose()
        {
            Mask.Dispose();
        }

        private Stream LoadFileStream()
        {
            var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (_values != null)
            {
                return new YARGSongFileStream(fs, _values);
            }
            return fs;
        }


        private const int XORMASK_SIZE = 16;
        private const int BYTES_64BIT = 8;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;
        private static readonly byte[] SNGPKG = { (byte)'S', (byte) 'N', (byte) 'G', (byte)'P', (byte)'K', (byte)'G' };

        public static SngFile? TryLoadFromFile(string path)
        {
            var result = InitStream_Internal(path);
            using var stream = result.stream;
            if (stream == null)
            {
                return null;
            }

            Span<byte> tag = stackalloc byte[SNGPKG.Length];
            if (stream.Read(tag) != tag.Length || !tag.SequenceEqual(SNGPKG))
            {
                return null;
            }

            try
            {
                uint version = stream.Read<uint>(Endianness.Little);
                var xorMask = stream.ReadBytes(XORMASK_SIZE);
                var metadata = ReadMetadata(stream);
                var listings = ReadListings(stream);
                return new SngFile(version, xorMask, metadata, path, listings, result.values);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error loading {path}.");
                return null;
            }
        }

        private static (Stream? stream, int[]? values) InitStream_Internal(string filename)
        {
            try
            {
                var filestream = File.OpenRead(filename);
                var values = YARGSongFileStream.TryParseYARGSongValues(filestream);
                if (values != null)
                {
                    return (new YARGSongFileStream(filestream, values), values);
                }

                filestream.Position = 0;
                return (filestream, null);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error loading {filename}");
                return (null, null);
            }
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
                    {
                        list.Add(mod);
                    }
                    else
                    {
                        modifiers.Add(node.outputName, new() { mod });
                    }
                    text.Position = text.Next;
                }
                else
                {
                    text.Position += size;
                }
            }
            return new IniSection(modifiers);
        }

        private static Dictionary<string, SngFileListing> ReadListings(Stream stream)
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
                {
                    filename = filename[idx..];
                }
                listings.Add(filename.ToLower(), new SngFileListing(reader));
            }
            return listings;
        }
    }
}
