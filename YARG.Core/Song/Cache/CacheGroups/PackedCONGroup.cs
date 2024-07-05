using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup, IUpgradeGroup
    {
        public readonly AbridgedFileInfo Info;
        public readonly CONFile ConFile;
        public readonly CONFileListing? SongDTA;
        public readonly CONFileListing? UpgradeDta;
        public Stream? Stream;

        private AllocatedArray<byte>? _songDTAData;
        private AllocatedArray<byte>? _upgradeDTAData;

        public override string Location => Info.FullName;
        public Dictionary<string, RBProUpgrade> Upgrades { get; } = new();

        public PackedCONGroup(CONFile conFile, AbridgedFileInfo info, string defaultPlaylist)
            : base(defaultPlaylist)
        {
            const string SONGSFILEPATH = "songs/songs.dta";
            const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

            Info = info;
            ConFile = conFile;
            conFile.TryGetListing(UPGRADESFILEPATH, out UpgradeDta);
            conFile.TryGetListing(SONGSFILEPATH, out SongDTA);
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var song = PackedRBCONEntry.TryLoadFromCache(in ConFile, nodeName, upgrades, stream, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }

        public override ReadOnlyMemory<byte> SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(SongDTA!.LastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void AddUpgrade(string name, RBProUpgrade upgrade) { lock (Upgrades) Upgrades[name] = upgrade; }

        public bool LoadUpgrades(out YARGTextContainer<byte> container)
        {
            if (UpgradeDta == null)
            {
                container = default;
                return false;
            }

            try
            {
                Stream = new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                _upgradeDTAData = UpgradeDta.LoadAllBytes(Stream);
                return YARGDTAReader.TryCreate(_upgradeDTAData, out container);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {UpgradeDta.Filename}");
                container = default;
                return false;
            }
        }

        public bool LoadSongs(out YARGTextContainer<byte> container)
        {
            if (SongDTA == null)
            {
                container = default;
                return false;
            }

            try
            {
                Stream ??= new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                _songDTAData = SongDTA.LoadAllBytes(Stream);
                return YARGDTAReader.TryCreate(_songDTAData, out container);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {SongDTA.Filename}");
                container = default;
                return false;
            }
        }

        public ReadOnlyMemory<byte> SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(Info.LastUpdatedTime.ToBinary());
            writer.Write(UpgradeDta!.LastWrite.ToBinary());
            writer.Write(Upgrades.Count);
            foreach (var upgrade in Upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void DisposeStreamAndSongDTA()
        {
            Stream?.Dispose();
            _songDTAData?.Dispose();
        }

        public void DisposeUpgradeDTA()
        {
            _upgradeDTAData?.Dispose();
        }
    }
}
