using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup
    {
        private readonly string _directory;
        private readonly DateTime _dtaLastWrite;
        public readonly List<string> updates = new();

        public UpdateGroup(string directory, DateTime dtaLastWrite)
        {
            _directory = directory;
            _dtaLastWrite = dtaLastWrite;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(_directory);
            writer.Write(_dtaLastWrite.ToBinary());
            writer.Write(updates.Count);
            for (int i = 0; i < updates.Count; ++i)
                writer.Write(updates[i]);
            return ms.ToArray();
        }
    }
}
