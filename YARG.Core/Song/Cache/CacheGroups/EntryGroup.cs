using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    internal interface IEntryGroup
    {
        public void Serialize(MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> indices);

        public static void SerializeGroups<TGroup>(FileStream fileStream, List<TGroup> groups, Dictionary<SongEntry, CacheWriteIndices> nodes)
            where TGroup : IEntryGroup
        {
            using var groupStream = new MemoryStream();
            fileStream.Write(groups.Count, Endianness.Little);
            for (int i = 0; i < groups.Count; i++)
            {
                groupStream.SetLength(0);
                groups[i].Serialize(groupStream, nodes);
                fileStream.Write((int) groupStream.Length, Endianness.Little);
                fileStream.Write(groupStream.GetBuffer(), 0, (int) groupStream.Length);
            }
        }
    }
}
