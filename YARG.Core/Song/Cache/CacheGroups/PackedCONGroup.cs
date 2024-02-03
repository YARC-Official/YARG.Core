using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup, IModificationGroup
    {
        public const string SONGSFILEPATH = "songs/songs.dta";
        public const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

        public readonly CONFile CONFile;
        public readonly DateTime CONLastUpdated;
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

        public PackedCONGroup(CONFile file, AbridgedFileInfo info, string defaultPlaylist)
            : base(info.FullName, defaultPlaylist)
        {
            CONFile = file;
            CONLastUpdated = info.LastUpdatedTime;
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var song = PackedRBCONMetadata.TryLoadFromCache(CONFile, nodeName, upgrades, reader, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }

        public override byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(songDTA!.lastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (upgradeLock) Upgrades[name] = upgrade; }

        public YARGDTAReader? LoadUpgrades()
        {
            upgradeDta = CONFile.TryGetListing(UPGRADESFILEPATH);
            if (upgradeDta == null)
                return null;
            return YARGDTAReader.TryCreate(upgradeDta, CONFile);
        }

        public YARGDTAReader? LoadSongs()
        {
            if (songDTA == null && !SetSongDTA())
                return null;
            return YARGDTAReader.TryCreate(songDTA!, CONFile);
        }

        public bool SetSongDTA()
        {
            songDTA = CONFile.TryGetListing(SONGSFILEPATH);
            return songDTA != null;
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(CONLastUpdated.ToBinary());
            writer.Write(upgradeDta!.lastWrite.ToBinary());
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
