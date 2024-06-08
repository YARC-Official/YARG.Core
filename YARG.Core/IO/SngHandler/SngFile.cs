using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    /// <summary>
    /// <see href="https://github.com/mdsitton/SngFileFormat">Documentation of SNG file type</see>
    /// </summary>
    public class SngFile : IEnumerable<KeyValuePair<string, SngFileListing>>
    {
        public readonly AbridgedFileInfo Info;
        public readonly uint Version;
        public readonly SngMask Mask;
        public readonly IniSection Metadata;

        private readonly Dictionary<string, SngFileListing> _listings;
        private readonly int[]? _values;

        private SngFile(AbridgedFileInfo info, FileStream stream)
        {
            Info = info;
            Version = stream.Read<uint>(Endianness.Little);
            Mask = new SngMask(stream);
            Metadata = ReadMetadata(stream);
            _listings = ReadListings(stream);

            if (stream is YARGSongFileStream yargSongStream)
                _values = yargSongStream.Values;
        }

        public SngFileListing this[string key] => _listings[key];
        public bool ContainsKey(string key) => _listings.ContainsKey(key);
        public bool TryGetValue(string key, out SngFileListing listing) => _listings.TryGetValue(key, out listing);

        public FileStream LoadFileStream()
        {
            if (_values != null)
            {
                return new YARGSongFileStream(Info.FullName, _values);
            }
            return new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

        IEnumerator<KeyValuePair<string, SngFileListing>> IEnumerable<KeyValuePair<string, SngFileListing>>.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }


        private const int BYTES_64BIT = 8;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;
        private static readonly byte[] SNGPKG = { (byte)'S', (byte) 'N', (byte) 'G', (byte)'P', (byte)'K', (byte)'G' };

        public static SngFile? TryLoadFromFile(AbridgedFileInfo file)
        {
            using var stream = InitStream_Internal(file.FullName);
            if (stream == null)
            {
                return null;
            }

            try
            {
                return new SngFile(file, stream);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error loading {file.FullName}.");
                return null;
            }
        }

        private static FileStream? InitStream_Internal(string filename)
        {
            try
            {
                var filestream = File.OpenRead(filename);
                using var wrapper = DisposableCounter.Wrap(filestream);

                var yargSongStream = YARGSongFileStream.TryLoad(filestream);
                if (yargSongStream != null)
                {
                    yargSongStream.Seek(SNGPKG.Length, SeekOrigin.Current);
                    return yargSongStream;
                }

                filestream.Position = 0;
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                if (filestream.Read(tag) != tag.Length || !tag.SequenceEqual(SNGPKG))
                {
                    return null;
                }
                return wrapper.Release();
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error loading {filename}");
                return null;
            }
        }

        private static IniSection ReadMetadata(Stream stream)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            ulong length = stream.Read<ulong>(Endianness.Little) - sizeof(ulong);
            ulong numPairs = stream.Read<ulong>(Endianness.Little);

            var validNodes = SongIniHandler.SONG_INI_DICTIONARY["[song]"];
            var text = new YARGTextContainer<byte>(stream.ReadBytes((int)length), 0);
            for (ulong i = 0; i < numPairs; i++)
            {
                int strLength = GetLength(text);
                var key = Encoding.UTF8.GetString(text.Data, text.Position, strLength);
                text.Position += strLength;

                strLength = GetLength(text);
                var next = text.Position + strLength;
                if (validNodes.TryGetValue(key, out var node))
                {
                    var mod = node.CreateSngModifier(text, strLength);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                    {
                        list.Add(mod);
                    }
                    else
                    {
                        modifiers.Add(node.outputName, new() { mod });
                    }
                }
                text.Position = next;
            }
            return new IniSection(modifiers);
        }

        private static Dictionary<string, SngFileListing> ReadListings(Stream stream)
        {
            ulong length = stream.Read<ulong>(Endianness.Little) - sizeof(ulong);
            ulong numListings = stream.Read<ulong>(Endianness.Little);

            Dictionary<string, SngFileListing> listings = new((int)numListings);

            var reader = BinaryReaderExtensions.Load(stream, (int)length);
            for (ulong i = 0; i < numListings; i++)
            {
                var strLen = reader.ReadByte();
                string filename = Encoding.UTF8.GetString(reader.ReadBytes(strLen));
                int idx = filename.LastIndexOf('/');
                if (idx != -1)
                {
                    filename = filename[idx..];
                }
                listings.Add(filename.ToLower(), new SngFileListing(filename, reader));
            }
            return listings;
        }

        private static int GetLength(YARGTextContainer<byte> container)
        {
            int length = BitConverter.ToInt32(container.Data, container.Position);
            container.Position += sizeof(int);
            if (container.Position + length > container.Length)
            {
                throw new EndOfStreamException();
            }
            return length;
        }
    }
}
