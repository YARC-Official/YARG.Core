using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpdateGroup : IModificationGroup
    {
        private readonly DateTime dtaLastWrite;
        public readonly List<string> updates = new();

        public UpdateGroup(DateTime dtaLastWrite)
        {
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] SerializeModifications(string directory)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(updates.Count);
            for (int i = 0; i < updates.Count; ++i)
                writer.Write(updates[i]);
            return ms.ToArray();
        }
    }
}
