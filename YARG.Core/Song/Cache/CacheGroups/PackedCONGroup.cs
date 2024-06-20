﻿using System;
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
        
        public Dictionary<string, IRBProUpgrade> Upgrades { get; } = new();

        public PackedCONGroup(CONFile conFile, AbridgedFileInfo info, string defaultPlaylist)
            : base(info.FullName, defaultPlaylist)
        {
            const string SONGSFILEPATH = "songs/songs.dta";
            const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

            Info = info;
            ConFile = conFile;
            conFile.TryGetListing(UPGRADESFILEPATH, out UpgradeDta);
            conFile.TryGetListing(SONGSFILEPATH, out SongDTA);
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var song = PackedRBCONEntry.TryLoadFromCache(in ConFile, nodeName, upgrades, reader, strings);
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
            writer.Write(SongDTA!.LastWrite.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }

        public void AddUpgrade(string name, IRBProUpgrade upgrade) { lock (Upgrades) Upgrades[name] = upgrade; }

        public YARGDTAReader? LoadUpgrades()
        {
            if (UpgradeDta == null)
            {
                return null;
            }

            try
            {
                Stream = new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                _upgradeDTAData = UpgradeDta.LoadAllBytes(Stream);
                return YARGDTAReader.TryCreate(_upgradeDTAData);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {UpgradeDta.Filename}");
                return null;
            }
        }

        public YARGDTAReader? LoadSongs()
        {
            if (SongDTA == null)
            {
                return null;
            }

            try
            {
                Stream ??= new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                _songDTAData = SongDTA.LoadAllBytes(Stream);
                return YARGDTAReader.TryCreate(_songDTAData);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {SongDTA.Filename}");
                return null;
            }
        }

        public byte[] SerializeModifications()
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
            return ms.ToArray();
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
