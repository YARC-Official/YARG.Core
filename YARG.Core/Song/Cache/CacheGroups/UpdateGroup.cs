using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            SubDirectories = directory.EnumerateDirectories().ToDictionary(dir => dir.Name.ToLowerInvariant());
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
        public readonly AbridgedFileInfo? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo? Milo;
        public readonly AbridgedFileInfo? Image;

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
                        Midi ??= new AbridgedFileInfo(file, false);
                    }
                    else if (filename == filenames[1])
                    {
                        Mogg ??= new AbridgedFileInfo(file, false);
                    }
                    else if (filename == filenames[2])
                    {
                        Milo ??= new AbridgedFileInfo(file, false);
                    }
                    else if (filename == filenames[3])
                    {
                        Image ??= new AbridgedFileInfo(file, false);
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

            static void WriteInfo(AbridgedFileInfo? info, BinaryWriter writer)
            {
                if (info != null)
                {
                    writer.Write(true);
                    writer.Write(info.LastUpdatedTime.ToBinary());
                }
                else
                {
                    writer.Write(false);
                }
            }
        }

        public bool Validate(BinaryReader reader)
        {
            if (!CheckInfo(Midi, reader))
            {
                goto FailedMidi;
            }

            if (!CheckInfo(Mogg, reader))
            {
                goto FailedMogg;
            }

            if (!CheckInfo(Milo, reader))
            {
                goto FailedMilo;
            }
            return CheckInfo(Image, reader);

        FailedMidi:
            SkipInfo(reader);
        FailedMogg:
            SkipInfo(reader);
        FailedMilo:
            SkipInfo(reader);
            return false;

            static bool CheckInfo(AbridgedFileInfo? info, BinaryReader reader)
            {
                if (reader.ReadBoolean())
                {
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    if (info == null || info.LastUpdatedTime != lastWrite)
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
