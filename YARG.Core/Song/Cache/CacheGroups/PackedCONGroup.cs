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

        public readonly string Filename;
        public readonly List<CONFileListing> Files;
        public readonly DateTime LastWrite;
        public readonly Dictionary<string, IRBProUpgrade> Upgrades = new();

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

        public PackedCONGroup(string filename, List<CONFileListing> files, DateTime lastWrite)
        {
            Filename = filename;
            this.Files = files;
            this.LastWrite = lastWrite;
        }

        public override bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.PackedRBCONFromCache(Files, nodeName, upgrades, reader, strings);
            if (song == null)
                return false;

            AddEntry(nodeName, index, song);
            return true;
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (upgradeLock) Upgrades[name] = upgrade; }

        public YARGDTAReader? LoadUpgrades()
        {
            upgradeDta = CONFileHandler.TryGetListing(Files, UPGRADESFILEPATH);
            if (upgradeDta == null)
                return null;

            return new YARGDTAReader(upgradeDta.LoadAllBytes());
        }

        public YARGDTAReader? LoadSongs()
        {
            if (songDTA == null && !SetSongDTA())
                return null;

            return new YARGDTAReader(songDTA!.LoadAllBytes());
        }

        public bool SetSongDTA()
        {
            songDTA = CONFileHandler.TryGetListing(Files, SONGSFILEPATH);
            return songDTA != null;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(upgradeDta!.ConFile.FullName);
            writer.Write(LastWrite.ToBinary());
            writer.Write(upgradeDta.lastWrite.ToBinary());
            writer.Write(Upgrades.Count);
            foreach (var upgrade in Upgrades)
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

            writer.Write(songDTA!.ConFile.FullName);
            writer.Write(songDTA!.lastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
