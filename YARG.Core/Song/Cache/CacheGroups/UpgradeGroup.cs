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
        private readonly string _directory;
        private readonly DateTime _dtaLastUpdate;
        public Dictionary<string, IRBProUpgrade> Upgrades { get; } = new();

        public UpgradeGroup(string directory, DateTime dtaLastUpdate)
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
