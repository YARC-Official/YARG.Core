using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpgradeGroup : IModificationGroup
    {
        private readonly string _directory;
        private readonly DateTime _dtaLastUpdate;
        public readonly Dictionary<string, IRBProUpgrade> upgrades = new();

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
