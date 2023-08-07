using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public interface IRBProUpgrade
    {
        public void WriteToCache(BinaryWriter writer);
        public bool Validate();
        public YARGFile? LoadUpgradeMidi();
    }

    public sealed class PackedRBProUpgrade
    {
        private readonly CONFile? conFile;
        private readonly FileListing? _midiListing;
        private readonly DateTime lastWrite;

        public FileListing MidiListing => _midiListing;

        public PackedRBProUpgrade(CONFile? conFile, FileListing? listing, DateTime lastWrite)
        {
            this.conFile = conFile;
            _midiListing = listing;
            this.lastWrite = listing != null ? listing.lastWrite : lastWrite;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(lastWrite.ToBinary());
        }

        public bool Validate()
        {
            return _midiListing != null && _midiListing.lastWrite == lastWrite;
        }

        public YARGFile? LoadUpgradeMidi()
        {
            if (!Validate())
                return null;
            return new YARGFile(conFile.LoadSubFile(_midiListing));
        }
    }

    public sealed class UnPackedRBProUpgrade
    {
        private AbridgedFileInfo _midiFile;
        public AbridgedFileInfo Midi => _midiFile;

        public UnPackedRBProUpgrade(string filename, DateTime lastWrite)
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

        public YARGFile? LoadUpgradeMidi()
        {
            if (!Validate())
                return null;
            return new YARGFile(_midiFile.FullName);
        }
    }
}
