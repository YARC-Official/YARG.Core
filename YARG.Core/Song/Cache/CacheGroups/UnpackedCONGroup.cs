using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public class UnpackedCONGroup : CONGroup
    {
        public readonly AbridgedFileInfo dta;
        public readonly YARGDTAReader reader;

        public UnpackedCONGroup(FileInfo dta)
        {
            this.dta = dta;
            reader = new(dta.FullName);
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.UnpackedRBCONFromCache(dta, nodeName, upgrades, reader, strings);
            if (song != null)
                AddEntry(nodeName, index, song);
        }

        public byte[] FormatEntriesForCache(string directory, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
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
