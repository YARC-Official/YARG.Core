using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public readonly struct FileCollection
    {
        public readonly DirectoryInfo Directory;
        public readonly Dictionary<string, FileInfo> Subfiles;
        public readonly Dictionary<string, DirectoryInfo> SubDirectories;

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
            Subfiles = new();
            SubDirectories = new();

            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                string name = info.Name.ToLower();
                switch (info)
                {
                    case FileInfo subFile:
                        Subfiles.Add(name, subFile);
                        break;
                    case DirectoryInfo subDirectory:
                        SubDirectories.Add(name, subDirectory);
                        break;
                }
            }
        }

        public bool ContainsAudio()
        {
            foreach (var subFile in Subfiles.Keys)
            {
                if (IniAudio.IsAudioFile(subFile))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
