using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public interface IModificationGroup
    {
        public ReadOnlyMemory<byte> SerializeModifications();

        public static void SerializeGroups<TGroup>(List<TGroup> groups, BinaryWriter writer)
            where TGroup : IModificationGroup
        {
            var spans = new ReadOnlyMemory<byte>[groups.Count];
            int length = sizeof(int);
            for (int i = 0; i < groups.Count; i++)
            {
                spans[i] = groups[i].SerializeModifications();
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
