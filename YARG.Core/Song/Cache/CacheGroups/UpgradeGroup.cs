using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public interface IUpgradeGroup : IModificationGroup
    {
        public Dictionary<string, IRBProUpgrade> Upgrades { get; }
    }

    public sealed class UpgradeGroup : IUpgradeGroup
    {
        public readonly string Directory;
        public readonly DateTime DtaLastUpdate;
        public Dictionary<string, IRBProUpgrade> Upgrades { get; } = new();

        public UpgradeGroup(string directory, DateTime dtaLastUpdate)
        {
            Directory = directory;
            DtaLastUpdate = dtaLastUpdate;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Directory);
            writer.Write(DtaLastUpdate.ToBinary());
            writer.Write(Upgrades.Count);
            foreach (var upgrade in Upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }
    }
}
