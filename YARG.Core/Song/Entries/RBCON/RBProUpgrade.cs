using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song
{
    public interface IRBProUpgrade
    {
        public DateTime LastUpdatedTime { get; }
        public void WriteToCache(BinaryWriter writer);
        public Stream? GetUpgradeMidiStream();
        public FixedArray<byte>? LoadUpgradeMidi();
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
            _lastUpdatedTime = listing?.LastWrite ?? lastWrite;
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

        public FixedArray<byte>? LoadUpgradeMidi()
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
        private readonly AbridgedFileInfo_Length _midi;
        public DateTime LastUpdatedTime => _midi.LastUpdatedTime;

        public UnpackedRBProUpgrade(in AbridgedFileInfo_Length info)
        {
            _midi = info;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_midi.LastUpdatedTime.ToBinary());
            writer.Write(_midi.Length);
        }

        public Stream? GetUpgradeMidiStream()
        {
            return _midi.IsStillValid() ? new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        }

        public FixedArray<byte>? LoadUpgradeMidi()
        {
            return _midi.IsStillValid() ? MemoryMappedArray.Load(_midi) : null;
        }
    }
}
