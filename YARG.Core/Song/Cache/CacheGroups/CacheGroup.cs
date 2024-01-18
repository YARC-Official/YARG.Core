using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface ICacheGroup
    {
        public int Count { get; }

        public byte[] SerializeEntries(string filename, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes);
        public bool TryRemoveEntry(SongMetadata entryToRemove);

        public static void SerializeGroups<TGroup>(List<(string Location, TGroup Group)> groups, BinaryWriter writer, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
            where TGroup : ICacheGroup
        {
            writer.Write(groups.Count);
            foreach (var (Location, Group) in groups)
            {
                byte[] buffer = Group.SerializeEntries(Location, nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }
}
