using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public class SongUpdate : IComparable<SongUpdate>
    {
        private readonly DateTime _dtaLastWrite;
        private readonly List<YARGDTAReader> _readers = new();

        public readonly string Directory;
        public readonly AbridgedFileInfo? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo? Milo;
        public readonly AbridgedFileInfo? Image;

        public YARGDTAReader[] Readers
        {
            get
            {
                var readers = new YARGDTAReader[_readers.Count];
                for (int i = 0; i < readers.Length; i++)
                {
                    readers[i] = _readers[i].Clone();
                }
                return readers;
            }
        }

        public SongUpdate(string directory, string name, in DateTime dtaLastWrite)
        {
            _dtaLastWrite = dtaLastWrite;

            Directory = directory;
            Midi = GetInfo(Path.Combine(directory, name + "_update.mid"));
            Mogg = GetInfo(Path.Combine(directory, name + "_update.mogg"));

            directory = Path.Combine(directory, "gen");
            Milo = GetInfo(Path.Combine(directory, name + ".milo_xbox"));
            Image = GetInfo(Path.Combine(directory, name + "_keep.png_xbox"));

            static AbridgedFileInfo? GetInfo(string filename)
            {
                var info = new FileInfo(filename);
                return info.Exists ? new AbridgedFileInfo(info, false) : null;
            }
        }

        public void AddReader(YARGDTAReader reader)
        {
            _readers.Add(reader);
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

        public static void SkipInfo(BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                reader.Move(SongMetadata.SIZEOF_DATETIME);
            }
        }
    }

    public sealed class UpdateGroup : IModificationGroup
    {
        private readonly string _directory;
        private readonly DateTime _dtaLastUpdate;
        public readonly List<string> updates = new();

        public UpdateGroup(string directory, DateTime dtaLastUpdate)
        {
            _directory = directory;
            _dtaLastUpdate = dtaLastUpdate;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(_directory);
            writer.Write(_dtaLastUpdate.ToBinary());
            writer.Write(updates.Count);
            for (int i = 0; i < updates.Count; ++i)
                writer.Write(updates[i]);
            return ms.ToArray();
        }
    }
}
