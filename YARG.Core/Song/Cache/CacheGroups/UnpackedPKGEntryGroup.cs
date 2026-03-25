using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal class UnpackedPKGEntryGroup : CONEntryGroup
    {
        protected override CONEntryType Tag => CONEntryType.UnpackedPKGEntry;

        private UnpackedPKGEntryGroup(in AbridgedFileInfo root, string defaultPlaylist)
            : base(root, defaultPlaylist) { }

        public override ScanExpected<RBCONEntry> CreateEntry(in RBScanParameters paramaters)
        {
            return UnpackedRBPKGEntry.Create(in paramaters);
        }

        public override void DeserializeEntry(ref FixedArrayStream stream, string name, int index, CacheReadStrings strings)
        {
            var entry = UnpackedRBPKGEntry.TryDeserialize(in _root, name, ref stream, strings);
            if (entry != null)
            {
                AddEntry(name, index, entry);
            }
        }

        public static bool Create(string directory, FileInfo dtaInfo, string defaultPlaylist, out CONEntryGroup group)
        {
            var dtaLastWrite = AbridgedFileInfo.NormalizedLastWrite(dtaInfo);
            var root = new AbridgedFileInfo(directory, dtaLastWrite);
            group = new UnpackedPKGEntryGroup(in root, defaultPlaylist);
            try
            {
                using var data = FixedArray.LoadFile(dtaInfo.FullName);
                var container = YARGDTAReader.Create(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    if (!group._nodes.TryGetValue(name, out var list))
                    {
                        // Most dtas abide by the `unique name` rule, so we only really need the space for one
                        //.... "MOST"
                        group._nodes.Add(name, list = new List<YARGTextContainer<byte>>(1));
                    }
                    list.Add(container);
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
    }
}
