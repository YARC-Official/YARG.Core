using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface IModificationGroup
    {
        public byte[] SerializeModifications();

        public static void SerializeGroups<TGroup>(List<TGroup> groups, BinaryWriter writer)
            where TGroup : IModificationGroup
        {
            writer.Write(groups.Count);
            foreach (var group in groups)
            {
                byte[] buffer = group.SerializeModifications();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }
    }
}
