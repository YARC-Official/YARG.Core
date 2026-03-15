using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    // This is adapted from Onyx's Haskell milo file parser
    public static class YARGMiloReader
    {
        /// <summary>
        /// Gets a file from milo data you supply.
        /// </summary>
        /// <param name="milo">CONFileStream pointing to the milo data</param>
        /// <param name="filename">The name of the file you're looking for <b>e.g.</b> song.anim</param>
        /// <returns>FixedArray&lt;byte&gt; containing the file data</returns>
        /// <remarks><b>WARNING</b>: You are responsible for disposing of the FixedArray when you are done with it!</remarks>
        public static FixedArray<byte> GetMiloFile(FixedArray<byte> milo, string filename)
        {
            using var decompressed = Decompress(milo);
            using var stream = decompressed.ToReferenceStream();
            // Get the directory structure
            var directory = ParseMiloDirectory(new BinaryReader(stream));

            var fileSpan = FindFile(directory, Encoding.UTF8.GetBytes(filename), decompressed.ReadOnlySpan);

            // Copy the span to a new FixedArray
            var fixedArray = FixedArray<byte>.Alloc(fileSpan.Length);
            fileSpan.CopyTo(fixedArray.Span);
            return fixedArray;
        }

        /// <summary>
        /// Finds a file in the milo data
        /// </summary>
        /// <param name="directory">A MiloDirectory</param>
        /// <param name="filename">Filename to fetch</param>
        /// <param name="data">The decompressed milo data</param>
        /// <returns>ReadOnlySpan&lt;byte&gt; containing the file data</returns>
        /// <remarks>WARNING: The returned Span is backed by a FixedArray. Copy the data if you need it to live for long.</remarks>
        private static ReadOnlySpan<byte> FindFile(MiloDirectory directory, ReadOnlySpan<byte> filename, ReadOnlySpan<byte> data)
        {
            for (var i = 0; i < directory.EntryNames.Count; i++)
            {
                var entry = directory.EntryNames[i];
                if (filename.SequenceEqual(entry.value))
                {
                    if (i >= directory.Files.Count)
                    {
                        YargLogger.LogFormatWarning("Inconsistent milo directory when searching for: {0}", Encoding.UTF8.GetString(filename));
                        return ReadOnlySpan<byte>.Empty;
                    }
                    var file = directory.Files[i];
                    return data.Slice(file.offset, file.length);
                }
            }

            // If we got here, it wasn't found at our level, so search subdirectories
            foreach (var subdir in directory.SubDirectories)
            {
                var file = FindFile(subdir, filename, data);
                if (!file.IsEmpty)
                {
                    return file;
                }
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static FixedArray<byte> Decompress(FixedArray<byte> miloFile)
        {
            Dictionary<uint, FileType> magicNumbers = new()
            {
                {0xCABEDEAF, FileType.MILO_A},
                {0xCBBEDEAF, FileType.MILO_B},
                {0xCCBEDEAF, FileType.MILO_C},
                {0xCDBEDEAF, FileType.MILO_D}
            };

            using var stream = miloFile.ToReferenceStream();
            var reader = new BinaryReader(stream);
            var magicNumber = reader.ReadUInt32();

            if (!magicNumbers.TryGetValue(magicNumber, out var type))
            {
                throw new InvalidDataException("Not a valid Milo file");
            }

            // Offset at which the actual data starts
            var dataOffset = reader.ReadUInt32();

            // Number of blocks in this file
            var blockCount = reader.ReadUInt32();

            // Largest (uncompressed) block in this file
            var largestBlock = reader.ReadUInt32();

            var blockInfo = new BlockInfo[blockCount];

            const uint maxSize = 1 << 24;

            for (var i = 0; i < blockCount; i++)
            {
                var blockSize = reader.ReadUInt32();
                var compressed = false;
                FileType blockType;
                switch (type)
                {
                    case FileType.MILO_A:
                        // Not compressed, so do nothing
                        break;
                    case FileType.MILO_D:
                        // Check if compressed by checking bit 24 of size
                        compressed = (blockSize & maxSize) == 0;
                        // Now calculate the _actual_ size of the block
                        blockSize &= ~maxSize;
                        break;
                    default:
                        // MiloB and MiloC are compressed
                        compressed = true;
                        break;
                }

                blockInfo[i] = new BlockInfo {Compressed = compressed, Size = blockSize };
            }

            // Seek to data_offset
            reader.BaseStream.Seek(dataOffset, SeekOrigin.Begin);

            // Read each block, decompressing if necessary
            using var output = new MemoryStream((int) largestBlock);
            foreach (var block in blockInfo)
            {
                var blockData = miloFile.ReadonlySlice((int) reader.BaseStream.Position, (int) block.Size);
                reader.BaseStream.Seek(block.Size, SeekOrigin.Current);
                if (block.Compressed)
                {
                    var decompressedBlock = DecompressBlock(type, blockData);
                    output.Write(decompressedBlock);
                }
                else
                {
                    output.Write(blockData);
                }
            }

            var fixedArray = FixedArray<byte>.Alloc((int) output.Length);
            output.Position = 0;
            var readBytes = output.Read(fixedArray.Span);
            YargLogger.AssertFormat(readBytes == fixedArray.Length, "Read {0} bytes, expected {1}", readBytes, fixedArray.Length);
            return fixedArray;
        }

        private static MiloDirectory ParseMiloDirectory(BinaryReader reader)
        {
            var baseStream = (UnmanagedMemoryStream) reader.BaseStream;

            // Create the directory object
            var directory = new MiloDirectory
            {
                Version = reader.ReadUInt32BE(),
                Type = reader.ReadStringBE(),
                Name = reader.ReadStringBE(),
                MiloU1 = reader.ReadUInt32BE(),
                MiloU2 = reader.ReadUInt32BE(),
            };

            // Deal with EntryNames

            var entryCount = reader.ReadUInt32BE();
            var expectedMatrixCount = 7;

            directory.EntryNames = new List<(byte[] name, byte[] value)>((int) entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                var name = reader.ReadStringBE();
                var value = reader.ReadStringBE();
                directory.EntryNames.Add((name, value));
            }

            // Read a bit more
            directory.MiloU3 = reader.ReadUInt32BE();

            // If the next uint is expectedMatrixCount, assume no optional fields and rewind
            var nextUint = reader.ReadUInt32BE();
            if (nextUint == expectedMatrixCount)
            {
                reader.BaseStream.Seek(-4, SeekOrigin.Current);
                // Be explicit so the code is easier to understand later
                directory.MiloU4 = null;
                directory.SubName = null;
            }
            else
            {
                // Otherwise, read the optional fields
                directory.MiloU4 = nextUint;
                directory.SubName = reader.ReadStringBE();
            }

            nextUint = reader.ReadUInt32BE();
            if (nextUint == expectedMatrixCount)
            {
                reader.BaseStream.Seek(-4, SeekOrigin.Current);
                directory.MiloU5 = null;
                directory.MiloU6 = null;
            }
            else
            {
                directory.MiloU5 = nextUint;
                directory.MiloU6 = reader.ReadUInt32BE();
            }

            // Now that we're done with the optional stuff, read the matrix and move the hell on to greener pastures
            var matrixCount = reader.ReadUInt32BE();
            directory.MiloMatrices = new List<List<float>>((int) matrixCount);

            for (var i = 0; i < matrixCount; i++)
            {
                var currentMatrix = new List<float>(12);
                for (var j = 0; j < 12; j++)
                {
                    currentMatrix.Add(reader.ReadSingleBE());
                }
                directory.MiloMatrices.Add(currentMatrix);
            }

            directory.MiloU7 = reader.ReadUInt32BE();
            directory.MiloU8 = reader.ReadByte();
            directory.MiloU9 = reader.ReadUInt32BE();

            var parentCount = reader.ReadUInt32BE();

            directory.MiloParents = new List<byte[]>((int) parentCount);
            for (var i = 0; i < parentCount; i++)
            {
                directory.MiloParents.Add(reader.ReadStringBE());
            }

            directory.MiloU10 = reader.ReadByte();

            var childCount = reader.ReadUInt32BE();
            directory.MiloChildren = new List<byte[]>((int) childCount);
            for (var i = 0; i < childCount; i++)
            {
                directory.MiloChildren.Add(reader.ReadStringBE());
            }

            var flagByte = reader.ReadByte();
            reader.BaseStream.Seek(-1, SeekOrigin.Current);
            if (flagByte == 1)
            {
                directory.MiloU11 = reader.ReadUInt16BE();
            }
            else
            {
                // Be explicit
                directory.MiloU11 = null;
            }

            directory.SubDirectories = new List<MiloDirectory>((int)childCount);
            for (var i = 0; i < childCount; i++)
            {
                // Recursive call to parse each subdirectory
                directory.SubDirectories.Add(ParseMiloDirectory(reader));
            }

            // Define the barrier that terminates the next few data blocks
            byte[] magicBarrier = { 0xAD, 0xDE, 0xAD, 0xDE };

            // Read the unknown bytes block
            directory.UnknownBytes = reader.ReadUntilBarrier(magicBarrier).ToArray();

            // Read the embedded files, each terminated by the barrier
            directory.Files = new List<(int offset, int length)>((int)entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                try
                {
                    var fileSpan = reader.ReadUntilBarrier(magicBarrier);
                    long currentPos = baseStream.Position - fileSpan.Length - magicBarrier.Length;
                    YargLogger.Assert(currentPos >= 0, "File offset is negative");
                    directory.Files.Add(((int) currentPos, fileSpan.Length));
                }
                catch (InvalidDataException)
                {
                    YargLogger.LogWarning("Couldn't load milo files. Bug wyrdough to fix whatever in CONFileStream.LoadFile is causing it to return bad data for the last block.");
                    break;
                }
            }

            return directory;
        }

        static ReadOnlySpan<byte> DecompressBlock(FileType compression, ReadOnlySpan<byte> data)
        {
            switch (compression)
            {
                case FileType.MILO_A:
                    return data;

                case FileType.MILO_B:
                    return DecompressZlib(data.ToArray());
                case FileType.MILO_C:
                    return DecompressGzip(data.ToArray());
                case FileType.MILO_D:
                    if (data.Length < 5)
                    {
                        throw new InvalidDataException("Data too small to be a valid MiloD");
                    }

                    // Strip first 4 bytes and last byte, then prepend zlib header
                    var strippedData = data[4..^1];
                    return DecompressZlib(strippedData.ToArray());
                default:
                    throw new NotSupportedException("Unknown block type");
            }
        }

        static Span<byte> DecompressGzip(byte[] data)
        {
            using var decompressed = new MemoryStream();
            // Take the data block and decompress it
            using var gzip = new GZipStream(new MemoryStream(data, false), CompressionMode.Decompress);
            gzip.CopyTo(decompressed);
            return decompressed.ToArray();
        }

        static Span<byte> DecompressZlib(byte[] data)
        {
            using var decompressed = new MemoryStream();
            using var deflate = new DeflateStream(new MemoryStream(data, false), CompressionMode.Decompress);
            deflate.CopyTo(decompressed);
            return decompressed.ToArray();
        }

        private class MiloDirectory
        {
            // Probably only ever 25 or 28 (pre-RB3, RB3+)
            public uint   Version;
            public byte[] Type;
            public byte[] Name;
            // Count of strings in this part
            public uint MiloU1;
            // Count of names + total length
            public uint                              MiloU2;
            public List<(byte[] name, byte[] value)> EntryNames;
            // Unknown
            public uint MiloU3;
            // Seems to be 2 for TBRB
            public uint?   MiloU4;
            public byte[]? SubName;
            // ???
            public uint? MiloU5;
            // ???
            public uint?             MiloU6;
            public List<List<float>> MiloMatrices;
            // Almost always 0
            public uint MiloU7;
            // Always 1
            public byte MiloU8;
            // Always 0
            public uint         MiloU9;
            public List<byte[]> MiloParents;
            // 0 in parent directory, usually 1 in subdirectory, but not always?
            public byte         MiloU10;
            public List<byte[]> MiloChildren;
            // In v25 always nothing, v28 is always 256 in root, nothing in subdirectories
            public ushort?                      MiloU11;
            public List<MiloDirectory>          SubDirectories;
            public byte[]                       UnknownBytes;
            public List<(int offset, int length)> Files;
        }

        struct BlockInfo
        {
            public FileType Type;
            public bool     Compressed;
            public uint     Size;
        }

        private enum FileType
        {
            MILO_A,
            MILO_B,
            MILO_C,
            MILO_D
        }
    }
}