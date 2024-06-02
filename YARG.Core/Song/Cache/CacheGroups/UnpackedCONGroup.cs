﻿using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UnpackedCONGroup : CONGroup
    {
        public readonly AbridgedFileInfo DTA;

        public UnpackedCONGroup(string directory, FileInfo dta, string defaultPlaylist)
            : base(directory, defaultPlaylist)
        {
            DTA = new AbridgedFileInfo(dta);
        }

        public bool TryLoadReader(out YARGDTAReader reader)
        {
            return YARGDTAReader.TryCreate(DTA.FullName, out reader);
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var song = UnpackedRBCONEntry.TryLoadFromCache(Location, DTA, nodeName, upgrades, reader, strings);
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
            writer.Write(DTA.LastUpdatedTime.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
