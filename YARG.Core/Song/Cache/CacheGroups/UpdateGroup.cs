using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup, IDisposable
    {
        public readonly DirectoryInfo Directory;
        public readonly DateTime DTALastWrite;
        public readonly Dictionary<string, SongUpdate> Updates = new();

        private readonly FixedArray<byte> _dtaData;

        public UpdateGroup(DirectoryInfo directory, DateTime dtaLastUpdate, in FixedArray<byte> dtaData)
        {
            Directory = directory;
            DTALastWrite = dtaLastUpdate;
            _dtaData = dtaData;
        }

        public ReadOnlyMemory<byte> SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Directory.FullName);
            writer.Write(DTALastWrite.ToBinary());
            writer.Write(Updates.Count);
            foreach (var (name, update) in Updates)
            {
                writer.Write(name);
                update.Serialize(writer);
            }
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void Dispose()
        {
            _dtaData.Dispose();
        }
    }

    public class SongUpdate
    {
        private readonly List<YARGTextContainer<byte>> _containers;

        public readonly string BaseDirectory;
        public readonly AbridgedFileInfo_Length? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo_Length? Milo;
        public readonly AbridgedFileInfo_Length? Image;

        public YARGTextContainer<byte>[] Containers => _containers.ToArray();

        internal SongUpdate(string directory, AbridgedFileInfo_Length? midi, AbridgedFileInfo? mogg, AbridgedFileInfo_Length? milo, AbridgedFileInfo_Length? image)
        {
            _containers = new();
            BaseDirectory = directory;
            Midi = midi;
            Mogg = mogg;
            Milo = milo;
            Image = image;
        }

        public void Add(in YARGTextContainer<byte> container)
        {
            _containers.Add(container);
        }

        public void Serialize(BinaryWriter writer)
        {
            WriteInfo(Midi, writer);
            WriteInfo(Mogg, writer);
            WriteInfo(Milo, writer);
            WriteInfo(Image, writer);

            static void WriteInfo<TInfo>(in TInfo? info, BinaryWriter writer)
                where TInfo : struct, IAbridgedInfo
            {
                if (info != null)
                {
                    writer.Write(true);
                    writer.Write(info.Value.LastUpdatedTime.ToBinary());
                }
                else
                {
                    writer.Write(false);
                }
            }
        }

        public bool Validate(UnmanagedMemoryStream stream)
        {
            if (!CheckInfo(in Midi, stream))
            {
                SkipInfo(stream);
                SkipInfo(stream);
                SkipInfo(stream);
                return false;
            }

            if (!CheckInfo(in Mogg, stream))
            {
                SkipInfo(stream);
                SkipInfo(stream);
                return false;
            }

            if (!CheckInfo(in Milo, stream))
            {
                SkipInfo(stream);
                return false;
            }
            return CheckInfo(in Image, stream);

            static bool CheckInfo<TInfo>(in TInfo? info, UnmanagedMemoryStream stream)
                where TInfo : struct, IAbridgedInfo
            {
                if (stream.ReadBoolean())
                {
                    var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                    if (info == null || info.Value.LastUpdatedTime != lastWrite)
                    {
                        return false;
                    }
                }
                else if (info != null)
                {
                    return false;
                }
                return true;
            }
        }

        public static void SkipRead(UnmanagedMemoryStream stream)
        {
            SkipInfo(stream);
            SkipInfo(stream);
            SkipInfo(stream);
            SkipInfo(stream);
        }

        private static void SkipInfo(UnmanagedMemoryStream stream)
        {
            if (stream.ReadBoolean())
            {
                stream.Position += CacheHandler.SIZEOF_DATETIME;
            }
        }
    }
}
