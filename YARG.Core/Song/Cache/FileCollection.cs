using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    internal readonly struct FileCollection : IEnumerable<KeyValuePair<string, FileSystemInfo>>
    {
        private readonly Dictionary<string, FileSystemInfo> _entries;
        public readonly string Directory;
        public readonly bool ContainedDupes;

        // Attribute maps to Remote Storage files (ex. oneDrive) that are not locally present
        private const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes) 0x00400000;
        private static readonly EnumerationOptions OPTIONS = new()
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | RECALL_ON_DATA_ACCESS
        };

        public FileCollection(DirectoryInfo directory)
        {
            Directory = directory.FullName;
            _entries = new Dictionary<string, FileSystemInfo>(StringComparer.InvariantCultureIgnoreCase);
            var dupes = new HashSet<string>();

            foreach (var entry in directory.EnumerateFileSystemInfos("*", OPTIONS))
            {
                string name = entry.Name.ToLowerInvariant();
                if (!_entries.TryAdd(name, entry))
                {
                    dupes.Add(name);
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
