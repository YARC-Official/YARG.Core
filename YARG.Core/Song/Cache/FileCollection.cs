using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public readonly struct FileCollection
    {
        public readonly DirectoryInfo Directory;
        public readonly Dictionary<string, FileInfo> subfiles;
        public readonly Dictionary<string, DirectoryInfo> subDirectories;

        internal FileCollection(DirectoryInfo directory)
        {
            Directory = directory;
            subfiles = new();
            subDirectories = new();

            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                string name = info.Name.ToLower();
                switch (info)
                {
                    case FileInfo subFile:
                        subfiles.Add(name, subFile);
                        break;
                    case DirectoryInfo subDirectory:
                        subDirectories.Add(name, subDirectory);
                        break;
                }
            }
        }

        public bool ContainsAudio()
        {
            foreach (var subFile in subfiles.Keys)
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
