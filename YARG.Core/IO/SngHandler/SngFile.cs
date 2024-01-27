﻿using System;
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
        public readonly string Filename;
        public readonly uint Version;
        public readonly SngMask Mask;
        public readonly IniSection Metadata;

        private readonly Dictionary<string, SngFileListing> _listings;
        private readonly int[]? _values;

        private SngFile(FileStream stream)
        {
            Filename = stream.Name;
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
                return new YARGSongFileStream(Filename, _values);
            }
            return new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

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


        private const int BYTES_64BIT = 8;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;
        private static readonly byte[] SNGPKG = { (byte)'S', (byte) 'N', (byte) 'G', (byte)'P', (byte)'K', (byte)'G' };

        public static SngFile? TryLoadFromFile(string path)
        {
            using var stream = InitStream_Internal(path);
            if (stream == null)
            {
                return null;
            }

            try
            {
                return new SngFile(stream);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error loading {path}.");
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
                YargTrace.LogException(ex, $"Error loading {filename}");
                return null;
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
                listings.Add(filename.ToLower(), new SngFileListing(filename, reader));
            }
            return listings;
        }
    }
}
