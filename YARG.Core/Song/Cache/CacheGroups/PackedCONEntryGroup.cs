using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal class PackedCONEntryGroup : CONEntryGroup
    {
        private readonly List<CONFileListing> _listings;
        private FileStream _stream = null!;

        protected override bool Tag => true;

        private PackedCONEntryGroup(List<CONFileListing> listings, in AbridgedFileInfo root, string defaultPlaylist)
            : base(root, defaultPlaylist)
        {
            _listings = listings;
        }

        public override CONEntryGroup InitScan()
        {
            _stream = new FileStream(_root.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return this;
        }

        public override ScanExpected<RBCONEntry> CreateEntry(in RBScanParameters paramaters)
        {
            return PackedRBCONEntry.Create(in paramaters, _listings, _stream);
        }

        public override void Dispose()
        {
            _stream.Dispose();
            base.Dispose();
        }

        public override void DeserializeEntry(ref FixedArrayStream stream, string name, int index, CacheReadStrings strings)
        {
            var entry = PackedRBCONEntry.TryDeserialize(_listings, in _root, name, ref stream, strings);
            if (entry != null)
            {
                AddEntry(name, index, entry);
            }
        }

        public static bool Create(Stream stream, List<CONFileListing> listings, in AbridgedFileInfo root, string defaultPlaylist, out PackedCONEntryGroup group)
        {
            const string SONGS_PATH = "songs/songs.dta";
            group = null!;
            if (listings.FindListing(SONGS_PATH, out var listing))
            {
                group = new PackedCONEntryGroup(listings, in root, defaultPlaylist);
                using var data = CONFileStream.LoadFile(stream, listing);
                var container = YARGDTAReader.Create(in data);
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
            }
            return group != null;
        }
    }
}
