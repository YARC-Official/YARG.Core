using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface ICacheGroup
    {
        public byte[] SerializeEntries(string filename, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes);

        public static void SerializeGroups<TGroup>(ICollection<KeyValuePair<string, TGroup>> groups, BinaryWriter writer, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
            where TGroup : ICacheGroup
        {
            writer.Write(groups.Count);
            foreach (var group in groups)
            {
                byte[] buffer = group.Value.SerializeEntries(group.Key, nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }
}
