using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface ICacheGroup<TMetadata>
        where TMetadata : SongMetadata
    {
        public int Count { get; }

        public byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes);
        public bool TryRemoveEntry(SongMetadata entryToRemove);

        public static void SerializeGroups<TGroup>(List<TGroup> groups, BinaryWriter writer, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
            where TGroup : ICacheGroup<TMetadata>
        {
            writer.Write(groups.Count);
            foreach (var group in groups)
            {
                byte[] buffer = group.SerializeEntries(nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }
}
