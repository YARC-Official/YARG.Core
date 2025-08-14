using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal class CONUpdateGroup : IDisposable
    {
        private readonly Dictionary<string, (List<YARGTextContainer<byte>> Containers, DateTime? Update)> _updates = new();
        private AbridgedFileInfo _root;
        private FixedArray<byte> _data = null!;

        public AbridgedFileInfo Root => _root;
        public Dictionary<string, (List<YARGTextContainer<byte>> Containers, DateTime? Update)> Updates => _updates;

        public void Dispose()
        {
            _data.Dispose();
        }

        private CONUpdateGroup() { }

        public static bool Create(string directory, FileInfo dtaInfo, out CONUpdateGroup group)
        {
            try
            {
                group = new CONUpdateGroup()
                {
                    _root = new AbridgedFileInfo(directory, AbridgedFileInfo.NormalizedLastWrite(dtaInfo)),
                };

                using var data = FixedArray.LoadFile(dtaInfo.FullName);

                var container = YARGDTAReader.Create(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    if (!group._updates.TryGetValue(name, out var node))
                    {
                        DateTime? lastWriteTime = null;
                        var info = new FileInfo(Path.Combine(group._root.FullName, name, name + "_update.mid"));
                        if (info.Exists)
                        {
                            lastWriteTime = AbridgedFileInfo.NormalizedLastWrite(info);
                        }
                        group._updates.Add(name, node = (new List<YARGTextContainer<byte>>(), lastWriteTime));
                    }
                    node.Containers.Add(container);
                    YARGDTAReader.EndNode(ref container);
                }
                group._data = data.TransferOwnership();
                return true;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                group = null!;
                return false;
            }
        }

        public static void SerializeGroups(FileStream fileStream, List<CONUpdateGroup> groups)
        {
            using var groupStream = new MemoryStream();
            fileStream.Write(groups.Count, Endianness.Little);
            for (int i = 0; i < groups.Count; i++)
            {
                groupStream.SetLength(0);
                groups[i]._root.Serialize(groupStream);
                groupStream.Write(groups[i]._updates.Count, Endianness.Little);
                foreach (var node in groups[i]._updates)
                {
                    groupStream.Write(node.Key);
                    groupStream.Write(node.Value.Update.HasValue);
                    if (node.Value.Update.HasValue)
                    {
                        groupStream.Write(node.Value.Update.Value.ToBinary(), Endianness.Little);
                    }
                }
                fileStream.Write((int) groupStream.Length, Endianness.Little);
                fileStream.Write(groupStream.GetBuffer(), 0, (int) groupStream.Length);
            }
        }
    }
}
