using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup, ICacheGroup, IModificationGroup
    {
        public const string SONGSFILEPATH = "songs/songs.dta";
        public const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

        public readonly CONFile file;
        public readonly DateTime lastWrite;
        public readonly Dictionary<string, IRBProUpgrade> upgrades = new();

        public int UpgradeCount => upgrades.Count;

        private readonly object upgradeLock = new();

        private CONFileListing? songDTA;
        private CONFileListing? upgradeDta;
        public DateTime DTALastWrite
        {
            get
            {
                if (songDTA == null)
                    return DateTime.MinValue;
                return songDTA.lastWrite;
            }
        }
        public DateTime UpgradeDTALastWrite
        {
            get
            {
                if (upgradeDta == null)
                    return DateTime.MinValue;
                return upgradeDta.lastWrite;
            }
        }

        public PackedCONGroup(CONFile file, DateTime lastWrite)
        {
            this.file = file;
            this.lastWrite = lastWrite;
        }

        public override bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.PackedRBCONFromCache(file, nodeName, upgrades, reader, strings);
            if (song == null)
                return false;

            AddEntry(nodeName, index, song);
            return true;
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }

        public YARGDTAReader? LoadUpgrades()
        {
            upgradeDta = file.TryGetListing(UPGRADESFILEPATH);
            if (upgradeDta == null)
                return null;

            return new YARGDTAReader(file.LoadSubFile(upgradeDta));
        }

        public YARGDTAReader? LoadSongs()
        {
            if (songDTA == null && !SetSongDTA())
                return null;

            return new YARGDTAReader(file.LoadSubFile(songDTA!));
        }

        public bool SetSongDTA()
        {
            songDTA = file.TryGetListing(SONGSFILEPATH);
            return songDTA != null;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(file.filename);
            writer.Write(lastWrite.ToBinary());
            writer.Write(upgradeDta!.lastWrite.ToBinary());
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }

        public byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(file.filename);
            writer.Write(songDTA!.lastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
