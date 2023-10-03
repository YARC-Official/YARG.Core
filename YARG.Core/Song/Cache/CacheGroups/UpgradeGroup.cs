using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpgradeGroup : IModificationGroup
    {
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, IRBProUpgrade> upgrades = new();

        public UpgradeGroup(DateTime dtaLastWrite)
        {
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] SerializeModifications(string directory)
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
