using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup, IUpgradeGroup
    {
        public readonly AbridgedFileInfo Info;
        public readonly CONFileListing[] Listings;
        public readonly CONFileListing? SongDTA;
        public readonly CONFileListing? UpgradeDta;

        public Stream? Stream;
        
        public Dictionary<string, IRBProUpgrade> Upgrades { get; } = new();

        public PackedCONGroup(CONFileListing[] listings, AbridgedFileInfo info, string defaultPlaylist)
            : base(info.FullName, defaultPlaylist)
        {
            const string SONGSFILEPATH = "songs/songs.dta";
            const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

            Info = info;
            Listings = listings;
            UpgradeDta = Listings.Find(UPGRADESFILEPATH);
            SongDTA = Listings.Find(SONGSFILEPATH);
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var song = PackedRBCONEntry.TryLoadFromCache(Listings, nodeName, upgrades, reader, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }

        public override byte[] SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(SongDTA!.lastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (Upgrades) Upgrades[name] = upgrade; }

        public bool TryLoadUpgradeReader(out YARGDTAReader reader)
        {
            if (UpgradeDta == null)
            {
                reader = default;
                return false;
            }
            Stream = new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return YARGDTAReader.TryCreate(UpgradeDta, Stream, out reader);
        }

        public bool TryLoadSongReader(out YARGDTAReader reader)
        {
            if (SongDTA == null)
            {
                reader = default;
                return false;
            }
            Stream ??= new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return YARGDTAReader.TryCreate(SongDTA, Stream, out reader);
        }

        public byte[] SerializeModifications()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(Info.LastUpdatedTime.ToBinary());
            writer.Write(UpgradeDta!.lastWrite.ToBinary());
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
