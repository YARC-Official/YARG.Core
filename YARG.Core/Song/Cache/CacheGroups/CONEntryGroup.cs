using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    internal abstract class CONEntryGroup : IEntryGroup, IDisposable, IEnumerable<KeyValuePair<string, List<YARGTextContainer<byte>>>>
    {
        public const string SONGS_DTA = "songs.dta";

        private readonly Dictionary<string, List<(int Index, RBCONEntry Entry)>> _entries;
        private readonly string _defaultPlaylist;
        protected readonly AbridgedFileInfo _root;
        protected readonly Dictionary<string, List<YARGTextContainer<byte>>> _nodes;
        protected FixedArray<byte> _data;

        protected abstract bool Tag { get; }

        public AbridgedFileInfo Root => _root;
        public string DefaultPlaylist => _defaultPlaylist;

        public abstract void DeserializeEntry(ref FixedArrayStream stream, string name, int index, CacheReadStrings strings);

        public virtual CONEntryGroup InitScan() { return this; }

        public abstract ScanExpected<RBCONEntry> CreateEntry(in RBScanParameters paramaters);

        public void AddEntry(string name, int index, RBCONEntry entry)
        {
            lock (_entries)
            {
                if (!_entries.TryGetValue(name, out var list))
                {
                    _entries.Add(name, list = new List<(int Index, RBCONEntry Entry)>(1));
                }

                int position = 0;
                while (position < list.Count && list[position].Index < index)
                {
                    ++position;
                }
                list.Insert(position, (index, entry));
            }
        }

        public void RemoveEntries(string name)
        {
            lock ( _entries)
            {
                _entries.Remove(name);
            }
        }

        public bool TryGetEntry(string name, int index, out RBCONEntry entry)
        {
            lock (_entries)
            {
                if (_entries.TryGetValue(name, out var list))
                {
                    for (int i = 0; i < list.Count; ++i)
                    {
                        if (list[i].Index == index)
                        {
                            entry = list[i].Entry;
                            return true;
                        }
                    }
                }
            }
            entry = null!;
            return false;
        }

        public void Serialize(MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> indices)
        {
            _root.Serialize(groupStream);
            groupStream.Write(Tag);

            int count = 0;
            foreach (var list in _entries)
            {
                count += list.Value.Count;
            }
            groupStream.Write(count, Endianness.Little);

            using var entryStream = new MemoryStream();
            foreach (var list in _entries)
            {
                foreach (var node in list.Value)
                {
                    entryStream.SetLength(0);

                    entryStream.Write(list.Key);
                    entryStream.WriteByte((byte)node.Index);
                    node.Entry.Serialize(entryStream, indices[node.Entry]);

                    groupStream.Write((int)entryStream.Length, Endianness.Little);
                    groupStream.Write(entryStream.GetBuffer(), 0, (int)entryStream.Length);
                }
            }
        }

        public virtual void Dispose()
        {
            _data.Dispose();
        }

        public Dictionary<string, List<YARGTextContainer<byte>>>.Enumerator GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, List<YARGTextContainer<byte>>>> IEnumerable<KeyValuePair<string, List<YARGTextContainer<byte>>>>.GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        protected CONEntryGroup(in AbridgedFileInfo root, string defaultPlaylist)
        {
            _root = root;
            _nodes = new Dictionary<string, List<YARGTextContainer<byte>>>();
            _entries = new Dictionary<string, List<(int Index, RBCONEntry Entry)>>();
            _defaultPlaylist = defaultPlaylist;
        }
    }
}
