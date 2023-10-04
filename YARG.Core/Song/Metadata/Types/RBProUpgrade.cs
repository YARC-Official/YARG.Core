using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public interface IRBProUpgrade
    {
        public DateTime LastWrite { get; }
        public void WriteToCache(BinaryWriter writer);
        public bool Validate();
        public byte[]? LoadUpgradeMidi();
    }

    [Serializable]
    public sealed class PackedRBProUpgrade : IRBProUpgrade
    {
        private readonly CONFile? conFile;
        private readonly CONFileListing? _midiListing;
        private readonly DateTime _lastWrite;

        public DateTime LastWrite => _lastWrite;

        public PackedRBProUpgrade(CONFile? conFile, CONFileListing? listing, DateTime lastWrite)
        {
            this.conFile = conFile;
            _midiListing = listing;
            _lastWrite = listing != null ? listing.lastWrite : lastWrite;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_lastWrite.ToBinary());
        }

        public bool Validate()
        {
            return _midiListing != null && _midiListing.lastWrite == _lastWrite;
        }

        public byte[]? LoadUpgradeMidi()
        {
            if (!Validate())
                return null;
            return conFile!.LoadSubFile(_midiListing!);
        }
    }

    [Serializable]
    public sealed class UnpackedRBProUpgrade : IRBProUpgrade
    {
        private AbridgedFileInfo _midiFile;
        public AbridgedFileInfo Midi => _midiFile;
        public DateTime LastWrite => _midiFile.LastWriteTime;

        public UnpackedRBProUpgrade(string filename, DateTime lastWrite)
        {
            _midiFile = new(filename, lastWrite);
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(_midiFile.LastWriteTime.ToBinary());
        }

        public bool Validate()
        {
            return _midiFile.IsStillValid();
        }

        public byte[]? LoadUpgradeMidi()
        {
            if (!Validate())
                return null;
            return File.ReadAllBytes(_midiFile.FullName);
        }
    }
}
