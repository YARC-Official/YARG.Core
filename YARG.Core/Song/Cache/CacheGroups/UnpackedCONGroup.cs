using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UnpackedCONGroup : CONGroup
    {
        public readonly AbridgedFileInfo DTA;

        public UnpackedCONGroup(string directory, FileInfo dta)
            : base(directory)
        {
            DTA = dta;
        }

        public YARGDTAReader? LoadDTA()
        {
            return YARGDTAReader.TryCreate(DTA.FullName);
        }

        public override bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var song = SongMetadata.UnpackedRBCONFromCache(DTA, nodeName, upgrades, reader, strings);
            if (song == null)
                return false;

            AddEntry(nodeName, index, song);
            return true;
        }

        public override byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(DTA.LastWriteTime.ToBinary());
            Serialize(writer, ref nodes);
            return ms.ToArray();
        }
    }
}
