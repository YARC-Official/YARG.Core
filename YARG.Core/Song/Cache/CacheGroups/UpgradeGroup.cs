using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public interface IUpgradeGroup : IModificationGroup
    {
        public Dictionary<string, RBProUpgrade> Upgrades { get; }
    }

    public sealed class UpgradeGroup : IUpgradeGroup, IDisposable
    {
        private readonly string _directory;
        private readonly DateTime _dtaLastUpdate;
        private readonly FixedArray<byte> _dtaData;

        public Dictionary<string, RBProUpgrade> Upgrades { get; } = new();

        public UpgradeGroup(string directory, DateTime dtaLastUpdate, in FixedArray<byte> dtaData)
        {
            _directory = directory;
            _dtaLastUpdate = dtaLastUpdate;
            _dtaData = dtaData;
        }

        public ReadOnlyMemory<byte> SerializeModifications()
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
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void Dispose()
        {
            _dtaData.Dispose();
        }
    }
}
