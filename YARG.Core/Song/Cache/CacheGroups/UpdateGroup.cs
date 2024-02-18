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
            var update = new SongUpdate(_directory, name, _dtaLastWrite, readers);
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

        public readonly string BaseDirectory;
        public readonly string UpdateDirectory;
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

        public SongUpdate(string directory, string name, DateTime dtaLastWrite, YARGDTAReader[] readers)
        {
            BaseDirectory = directory;
            UpdateDirectory = Path.Combine(directory, name);

            _dtaLastWrite = dtaLastWrite;
            _readers = readers;

            string basename = Path.Combine(UpdateDirectory, name);
            var file = new FileInfo(basename + "_update.mid");
            if (file.Exists)
            {
                Midi = new AbridgedFileInfo(file, false);
            }

            file = new FileInfo(basename + "_update.mogg");
            if (file.Exists)
            {
                Mogg = new AbridgedFileInfo(file, false);
            }

            basename = Path.Combine(UpdateDirectory, "gen", name);
            file = new FileInfo(basename + ".milo_xbox");
            if (file.Exists)
            {
                Milo = new AbridgedFileInfo(file, false);
            }

            file = new FileInfo(basename + "_keep.png_xbox");
            if (file.Exists)
            {
                Image = new AbridgedFileInfo(file, false);
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
