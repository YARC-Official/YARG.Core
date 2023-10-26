using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UnpackedCONGroup : CONGroup, ICacheGroup
    {
        public readonly AbridgedFileInfo dta;

        public UnpackedCONGroup(FileInfo dta)
        {
            this.dta = dta;
        }

        public YARGDTAReader? LoadDTA()
        {
            return YARGDTAReader.TryCreate(dta.FullName);
        }

        public override bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.UnpackedRBCONFromCache(dta, nodeName, upgrades, reader, strings);
            if (song == null)
                return false;

            AddEntry(nodeName, index, song);
            return true;
        }

        public byte[] SerializeEntries(string directory, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dta.LastWriteTime.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
