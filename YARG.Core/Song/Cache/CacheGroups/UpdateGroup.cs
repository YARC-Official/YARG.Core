using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup
    {
        private readonly string _directory;
        private readonly DateTime _dtaLastWrite;
        public readonly Dictionary<string, SongUpdate> Updates = new();

        public UpdateGroup(string directory, DateTime dtaLastUpdate)
        {
            _directory = directory;
            _dtaLastWrite = dtaLastUpdate;
        }

        public SongUpdate Add(string name, YARGDTAReader[] readers)
        {
            SongUpdateFiles? files = null;
            var dirInfo = new DirectoryInfo(Path.Combine(_directory, name));
            if (dirInfo.Exists)
            {
                files = new SongUpdateFiles(dirInfo, name.ToLowerInvariant());
            }

            var update = new SongUpdate(_directory, _dtaLastWrite, readers, files);
            lock (Updates)
            {
                Updates.Add(name, update);
            }
            return update;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(_directory);
            writer.Write(_dtaLastWrite.ToBinary());
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

        public readonly string Directory;
        public readonly SongUpdateFiles? Files;

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

        public SongUpdate(string directory, in DateTime dtaLastWrite, YARGDTAReader[] readers, SongUpdateFiles? files)
        {
            _dtaLastWrite = dtaLastWrite;
            _readers = readers;

            Directory = directory;
            Files = files;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Files != null);
            Files?.Serialize(writer);
        }

        public bool Validate(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return Files == null;
            }

            if (Files == null)
            {
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
                return false;
            }
            return Files.Validate(reader);
        }

        public int CompareTo(SongUpdate other)
        {
            return _dtaLastWrite.CompareTo(other._dtaLastWrite);
        }

        public static void SkipRead(BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
                SongUpdateFiles.SkipInfo(reader);
            }
        }
    }

    public sealed class SongUpdateFiles
    {
        public readonly string Directory;
        public readonly AbridgedFileInfo? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo? Milo;
        public readonly AbridgedFileInfo? Image;

        public SongUpdateFiles(DirectoryInfo directory, string name)
        {
            Directory = directory.FullName;
            var files = new (string Name, AbridgedFileInfo? Info)[]
            {
                (name + "_update.mid", null),
                (name + "_update.mogg", null),
                (name + ".milo_xbox", null),
                (name + "_keep.png_xbox", null)
            };

            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                string filename = info.Name.ToLowerInvariant();
                switch (info)
                {
                    case FileInfo file:
                        if (filename == files[0].Name)
                        {
                            files[0].Info = new AbridgedFileInfo(file, false);
                        }
                        else if (filename == files[1].Name)
                        {
                            files[1].Info = new AbridgedFileInfo(file, false);
                        }
                        break;
                    case DirectoryInfo subDirectory:
                        if (filename != "gen")
                            break;

                        foreach (var file in subDirectory.EnumerateFiles())
                        {
                            filename = file.Name.ToLowerInvariant();
                            if (filename == files[2].Name)
                            {
                                files[2].Info = new AbridgedFileInfo(file, false);
                            }
                            else if (filename == files[3].Name)
                            {
                                files[3].Info = new AbridgedFileInfo(file, false);
                            }
                        }
                        break;
                }
            }

            Midi = files[0].Info;
            Mogg = files[1].Info;
            Milo = files[2].Info;
            Image = files[3].Info;
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

        public static void SkipInfo(BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                reader.Move(CacheHandler.SIZEOF_DATETIME);
            }
        }
    }
}
