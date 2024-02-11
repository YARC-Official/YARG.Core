using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public class SongUpdate
    {
        public readonly string Name;
        public readonly AbridgedFileInfo? Midi;
        public readonly AbridgedFileInfo? Mogg;
        public readonly AbridgedFileInfo? Milo;
        public readonly AbridgedFileInfo? Image;

        public SongUpdate(string baseDirectory, string name)
        {
            Name = name;

            string dir = Path.Combine(baseDirectory, name);
            Midi = Get(Path.Combine(dir, name + "_update.mid"));
            Mogg = Get(Path.Combine(dir, name + "_update.mogg"));

            dir = Path.Combine(dir, "gen");
            Milo = Get(Path.Combine(dir, name + ".milo_xbox"));
            Image = Get(Path.Combine(dir, name + "_keep.png_xbox"));
        }

        private AbridgedFileInfo? Get(string filename)
        {
            var imageInfo = new FileInfo(filename);
            return imageInfo.Exists ? new AbridgedFileInfo(imageInfo, false) : null;
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
