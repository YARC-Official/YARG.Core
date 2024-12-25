using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public readonly struct FileCollection : IEnumerable<KeyValuePair<string, FileSystemInfo>>
    {
        private readonly Dictionary<string, FileSystemInfo> _entries;
        public readonly DirectoryInfo Directory;
        public readonly bool ContainedDupes;

        internal static bool TryCollect(string directory, out FileCollection collection)
        {
            var info = new DirectoryInfo(directory);
            if (!info.Exists)
            {
                collection = default;
                return false;
            }
            collection = new FileCollection(info);
            return true;
        }

        internal FileCollection(DirectoryInfo directory)
        {
            Directory = directory;
            _entries = new Dictionary<string, FileSystemInfo>(StringComparer.InvariantCultureIgnoreCase);
            var dupes = new HashSet<string>();

            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (!_entries.TryAdd(entry.Name, entry))
                {
                    dupes.Add(entry.Name);
                }
            }

            // Removes any sort of ambiguity from duplicates
            ContainedDupes = dupes.Count > 0;
            foreach (var dupe in dupes)
            {
                _entries.Remove(dupe);
            }
        }

        public bool FindFile(string name, out FileInfo file)
        {
            if (_entries.TryGetValue(name, out var entry) && entry is FileInfo result)
            {
                file = result;
                return true;
            }
            file = null!;
            return false;
        }

        public bool FindDirectory(string name, out DirectoryInfo directory)
        {
            if (_entries.TryGetValue(name, out var entry) && entry is DirectoryInfo result)
            {
                directory = result;
                return true;
            }
            directory = null!;
            return false;
        }

        public bool ContainsDirectory()
        {
            foreach (var entry in _entries)
            {
                if (entry.Value.Attributes == FileAttributes.Directory)
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsAudio()
        {
            foreach (var entry in _entries)
            {
                if (IniAudio.IsAudioFile(entry.Key))
                {
                    return true;
                }
            }
            return false;
        }

        public IEnumerator<KeyValuePair<string, FileSystemInfo>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, FileSystemInfo>>) _entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _entries).GetEnumerator();
        }
    }
}
