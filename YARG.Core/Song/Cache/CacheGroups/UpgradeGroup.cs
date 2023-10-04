using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpgradeGroup : IModificationGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, IRBProUpgrade> upgrades = new();

        public UpgradeGroup(string directory, DateTime dtaLastWrite)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }
    }
}
