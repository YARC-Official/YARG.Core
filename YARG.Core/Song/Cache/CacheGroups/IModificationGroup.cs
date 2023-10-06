using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface IModificationGroup
    {
        public byte[] SerializeModifications(string filename);

        public static void SerializeGroups<TGroup>(ICollection<KeyValuePair<string, TGroup>> groups, BinaryWriter writer)
            where TGroup : IModificationGroup
        {
            writer.Write(groups.Count);
            foreach (var group in groups)
            {
                byte[] buffer = group.Value.SerializeModifications(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }
}
