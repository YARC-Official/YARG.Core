﻿using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup, ICacheGroup, IModificationGroup
    {
        public const string SONGSFILEPATH = "songs/songs.dta";
        public const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

        public readonly DateTime LastWrite;
        public readonly CONScanResult CONFile;
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

        public PackedCONGroup(CONScanResult file, DateTime lastWrite)
        {
            CONFile = file;
            LastWrite = lastWrite;
        }

        public override bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.PackedRBCONFromCache(CONFile.Listings, nodeName, upgrades, reader, strings);
            if (song == null)
                return false;

            AddEntry(nodeName, index, song);
            return true;
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (upgradeLock) Upgrades[name] = upgrade; }

        public YARGDTAReader? LoadUpgrades()
        {
            upgradeDta = CONFileHandler.TryGetListing(CONFile.Listings, UPGRADESFILEPATH);
            if (upgradeDta == null)
                return null;
            return YARGDTAReader.TryCreate(upgradeDta, CONFile.Stream);
        }

        public YARGDTAReader? LoadSongs()
        {
            if (songDTA == null && !SetSongDTA())
                return null;
            return YARGDTAReader.TryCreate(songDTA!, CONFile.Stream);
        }

        public bool SetSongDTA()
        {
            songDTA = CONFileHandler.TryGetListing(CONFile.Listings, SONGSFILEPATH);
            return songDTA != null;
        }

        public byte[] SerializeModifications(string filename)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filename);
            writer.Write(LastWrite.ToBinary());
            writer.Write(upgradeDta!.lastWrite.ToBinary());
            writer.Write(Upgrades.Count);
            foreach (var upgrade in Upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }

        public byte[] SerializeEntries(string filename, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filename);
            writer.Write(songDTA!.lastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
