﻿using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public sealed class UnpackedCONGroup : CONGroup, IDisposable
    {
        public readonly AbridgedFileInfo_Length DTA;
        private MemoryMappedArray? _fileData;

        public override string Location { get; }

        public UnpackedCONGroup(string directory, FileInfo dta, string defaultPlaylist)
            : base(defaultPlaylist)
        {
            Location = directory;
            DTA = new AbridgedFileInfo_Length(dta);
        }

        public bool LoadDTA(out YARGTextContainer<byte> container)
        {
            try
            {
                _fileData = MemoryMappedArray.Load(DTA);
                return YARGDTAReader.TryCreate(_fileData, out container);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {DTA.FullName}");
                container = default;
                return false;
            }
        }

        public override void ReadEntry(string nodeName, int index, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var song = UnpackedRBCONEntry.TryLoadFromCache(Location, DTA, nodeName, upgrades, stream, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }

        public override ReadOnlyMemory<byte> SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(DTA.LastUpdatedTime.ToBinary());
            Serialize(writer, ref nodes);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void Dispose()
        {
            _fileData?.Dispose();
        }
    }
}
