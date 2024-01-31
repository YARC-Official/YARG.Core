﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public interface IRBProUpgrade
    {
        public DateTime LastUpdatedTime { get; }
        public void WriteToCache(BinaryWriter writer);
        public Stream? GetUpgradeMidiStream();
        public byte[]? LoadUpgradeMidi();
    }

    [Serializable]
    public sealed class PackedRBProUpgrade : IRBProUpgrade
    {
        private readonly CONFileListing? _midiListing;
        private readonly DateTime _lastUpdatedTime;

        public DateTime LastUpdatedTime => _lastUpdatedTime;

        public PackedRBProUpgrade(CONFileListing? listing, DateTime lastWrite)
        {
            _midiListing = listing;
            _lastUpdatedTime = listing?.lastWrite ?? lastWrite;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_lastUpdatedTime.ToBinary());
        }

        public Stream? GetUpgradeMidiStream()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return null;
            }
            return _midiListing.CreateStream();
        }

        public byte[]? LoadUpgradeMidi()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return null;
            }
            return _midiListing.LoadAllBytes();
        }
    }

    [Serializable]
    public sealed class UnpackedRBProUpgrade : IRBProUpgrade
    {
        private AbridgedFileInfo _midiFile;
        public AbridgedFileInfo Midi => _midiFile;
        public DateTime LastUpdatedTime => _midiFile.LastUpdatedTime;

        public UnpackedRBProUpgrade(AbridgedFileInfo info)
        {
            _midiFile = info;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_midiFile.LastUpdatedTime.ToBinary());
        }

        public Stream? GetUpgradeMidiStream()
        {
            return _midiFile.IsStillValid() ? new FileStream(_midiFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        }

        public byte[]? LoadUpgradeMidi()
        {
            return _midiFile.IsStillValid() ? File.ReadAllBytes(_midiFile.FullName) : null;
        }
    }
}
