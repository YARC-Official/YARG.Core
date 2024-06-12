using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup
    {
        public readonly DirectoryInfo Directory;
        public readonly DateTime DTALastWrite;
        public readonly Dictionary<string, DirectoryInfo> SubDirectories;
        public readonly Dictionary<string, SongUpdate> Updates = new();

        public UpdateGroup(DirectoryInfo directory, DateTime dtaLastUpdate)
        {
            Directory = directory;
            DTALastWrite = dtaLastUpdate;
            SubDirectories = new Dictionary<string, DirectoryInfo>();
            foreach (var dir in directory.EnumerateDirectories())
            {
                SubDirectories.Add(dir.Name, dir);
            }
        }

        public byte[] SerializeModifications()
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
            return ms.ToArray();
        }
    }

    public sealed class SongUpdate : IComparable<SongUpdate>
    {
        private readonly DateTime _dtaLastWrite;
        private readonly YARGDTAReader[] _readers;

        public readonly string BaseDirectory;
        public readonly AbridgedFileInfo_Length? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo_Length? Milo;
        public readonly AbridgedFileInfo_Length? Image;

        public YARGDTAReader[] Readers
        {
            get
            {
                var readers = new YARGDTAReader[_readers.Length];
                for (int i = 0; i < readers.Length; i++)
                {
                    readers[i] = _readers[i].Clone();
                }
                return readers;
            }
        }

        public SongUpdate(UpdateGroup group, string name, DateTime dtaLastWrite, YARGDTAReader[] readers)
        {
            BaseDirectory = group.Directory.FullName;
            _dtaLastWrite = dtaLastWrite;
            _readers = readers;

            string subname = name.ToLowerInvariant();
            if (group.SubDirectories.TryGetValue(subname, out var subDirectory))
            {
                var filenames = new string[]
                {
                    subname + "_update.mid",
                    subname + "_update.mogg",
                    subname + ".milo_xbox",
                    subname + "_keep.png_xbox"
                };

                foreach (var file in subDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    string filename = file.Name.ToLowerInvariant();
                    if (filename == filenames[0])
                    {
                        Midi ??= new AbridgedFileInfo_Length(file, false);
                    }
                    else if (filename == filenames[1])
                    {
                        Mogg ??= new AbridgedFileInfo(file, false);
                    }
                    else if (filename == filenames[2])
                    {
                        Milo ??= new AbridgedFileInfo_Length(file, false);
                    }
                    else if (filename == filenames[3])
                    {
                        Image ??= new AbridgedFileInfo_Length(file, false);
                    }
                }
            }
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

        public bool Validate(BinaryReader reader)
        {
            if (!CheckInfo(in Midi, reader))
            {
                SkipInfo(reader);
                SkipInfo(reader);
                SkipInfo(reader);
                return false;
            }

            if (!CheckInfo(in Mogg, reader))
            {
                SkipInfo(reader);
                SkipInfo(reader);
                return false;
            }

            if (!CheckInfo(in Milo, reader))
            {
                SkipInfo(reader);
                return false ;
            }
            return CheckInfo(in Image, reader);

            static bool CheckInfo<TInfo>(in TInfo? info, BinaryReader reader)
                where TInfo : struct, IAbridgedInfo
            {
                if (reader.ReadBoolean())
                {
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());
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

        public int CompareTo(SongUpdate other)
        {
            return _dtaLastWrite.CompareTo(other._dtaLastWrite);
        }

        public static void SkipRead(BinaryReader reader)
        {
            SkipInfo(reader);
            SkipInfo(reader);
            SkipInfo(reader);
            SkipInfo(reader);
        }

        private static void SkipInfo(BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                reader.Move(CacheHandler.SIZEOF_DATETIME);
            }
        }
    }
}
