using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;

namespace YARG.Core.Song
{
    public abstract class RBProUpgrade
    {
        public abstract DateTime LastUpdatedTime { get; }
        public abstract void WriteToCache(BinaryWriter writer);
        public abstract Stream? GetUpgradeMidiStream();
        public abstract FixedArray<byte>? LoadUpgradeMidi();
    }

    [Serializable]
    public sealed class PackedRBProUpgrade : RBProUpgrade
    {
        private readonly CONFileListing? _midiListing;
        private readonly DateTime _lastUpdatedTime;

        public override DateTime LastUpdatedTime => _lastUpdatedTime;

        public PackedRBProUpgrade(CONFileListing? listing, DateTime lastWrite)
        {
            _midiListing = listing;
            _lastUpdatedTime = listing?.LastWrite ?? lastWrite;
        }

        public override void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_lastUpdatedTime.ToBinary());
        }

        public override Stream? GetUpgradeMidiStream()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return null;
            }
            return _midiListing.CreateStream();
        }

        public override FixedArray<byte>? LoadUpgradeMidi()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return null;
            }
            return _midiListing.LoadAllBytes();
        }
    }

    [Serializable]
    public sealed class UnpackedRBProUpgrade : RBProUpgrade
    {
        private readonly AbridgedFileInfo_Length _midi;
        public override DateTime LastUpdatedTime => _midi.LastUpdatedTime;

        public UnpackedRBProUpgrade(in AbridgedFileInfo_Length info)
        {
            _midi = info;
        }

        public override void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_midi.LastUpdatedTime.ToBinary());
            writer.Write(_midi.Length);
        }

        public override Stream? GetUpgradeMidiStream()
        {
            return _midi.IsStillValid() ? new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        }

        public override FixedArray<byte>? LoadUpgradeMidi()
        {
            return _midi.IsStillValid() ? MemoryMappedArray.Load(_midi) : null;
        }
    }
}
