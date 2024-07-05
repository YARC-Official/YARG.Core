using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface ICacheGroup<TEntry>
        where TEntry : SongEntry
    {
        public int Count { get; }

        public ReadOnlyMemory<byte> SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes);
        public bool TryRemoveEntry(SongEntry entryToRemove);

        public static void SerializeGroups<TGroup>(List<TGroup> groups, BinaryWriter writer, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
            where TGroup : ICacheGroup<TEntry>
        {
            var spans = new ReadOnlyMemory<byte>[groups.Count];
            int length = 4;
            for (int i = 0; i < groups.Count; i++)
            {
                spans[i] = groups[i].SerializeEntries(nodes);
                length += sizeof(int) + spans[i].Length;
            }

            writer.Write(length);
            writer.Write(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                var span = spans[i].Span;
                writer.Write(span.Length);
                writer.Write(span);
            }
        }
    }
}
