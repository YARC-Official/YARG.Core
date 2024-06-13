using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup, IDisposable
    {
        public readonly DirectoryInfo Directory;
        public readonly DateTime DTALastWrite;
        public readonly Dictionary<string, DirectoryInfo> SubDirectories;
        public readonly Dictionary<string, SongUpdate> Updates = new();

        private readonly MemoryMappedArray _dtaData;

        public UpdateGroup(DirectoryInfo directory, DateTime dtaLastUpdate, MemoryMappedArray dtaData)
        {
            Directory = directory;
            DTALastWrite = dtaLastUpdate;
            _dtaData = dtaData;
            
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

        public void Dispose()
        {
            _dtaData.Dispose();
        }
    }

    public readonly struct SongUpdate : IComparable<SongUpdate>
    {
        private readonly DateTime _dtaLastWrite;

        public readonly string BaseDirectory;
        public readonly AbridgedFileInfo_Length? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo_Length? Milo;
        public readonly AbridgedFileInfo_Length? Image;
        public readonly YARGDTAReader[] Readers;

        public SongUpdate(UpdateGroup group, string name, DateTime dtaLastWrite, YARGDTAReader[] readers)
        {
            BaseDirectory = group.Directory.FullName;
            Readers = readers;
            _dtaLastWrite = dtaLastWrite;

            Midi = null;
            Mogg = null;
            Milo = null;
            Image = null;
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
                    string filename = file.Name;
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

        public int CompareTo(SongUpdate other)
        {
            return _dtaLastWrite.CompareTo(other._dtaLastWrite);
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
