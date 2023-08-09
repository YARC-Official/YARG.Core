using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public class PackedCONGroup : CONGroup
    {
        public const string SONGSFILEPATH = "songs/songs.dta";
        public const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

        public readonly CONFile file;
        public readonly DateTime lastWrite;
        public readonly Dictionary<string, IRBProUpgrade> upgrades = new();

        public int UpgradeCount => upgrades.Count;

        private readonly object upgradeLock = new();

        private FileListing? songDTA;
        private FileListing? upgradeDta;
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

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.PackedRBCONFromCache(file, nodeName, upgrades, reader, strings);
            if (song != null)
                AddEntry(nodeName, index, song);
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }

        public bool LoadUpgrades(out YARGDTAReader? reader)
        {
            upgradeDta = file.TryGetListing(UPGRADESFILEPATH);
            if (upgradeDta == null)
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(upgradeDta));
            return true;
        }

        public bool LoadSongs(out YARGDTAReader? reader)
        {
            if (songDTA == null && !SetSongDTA())
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(songDTA!));
            return true;
        }

        public bool SetSongDTA()
        {
            songDTA = file.TryGetListing(SONGSFILEPATH);
            return songDTA != null;
        }

        public byte[] FormatUpgradesForCache()
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

        public byte[] FormatEntriesForCache(ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
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
