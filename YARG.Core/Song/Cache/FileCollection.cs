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
            MatchType = MatchType.Win32,
            AttributesToSkip = RECALL_ON_DATA_ACCESS,
            IgnoreInaccessible = false,
        };

        public FileCollection(DirectoryInfo directory)
        {
            Directory = directory.FullName;
            _entries = new Dictionary<string, FileSystemInfo>(StringComparer.Ordinal);
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
            file = null!;
            if (_entries.TryGetValue(name, out var entry))
            {
                file = (entry as FileInfo)!;
            }
            return file != null!;
        }

        public bool FindDirectory(string name, out DirectoryInfo directory)
        {
            directory = null!;
            if (_entries.TryGetValue(name, out var entry))
            {
                directory = (entry as DirectoryInfo)!;
            }
            return directory != null!;
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

        public Dictionary<string, FileSystemInfo>.Enumerator GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, FileSystemInfo>> IEnumerable<KeyValuePair<string, FileSystemInfo>>.GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _entries.GetEnumerator();
        }
    }
}
