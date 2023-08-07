using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public sealed class RBPackedCONMetadata : IRBCONMetadata
    {
        private readonly CONFile conFile;
        private readonly FileListing midiListing;
        private readonly FileListing? moggListing;
        private readonly FileListing? miloListing;
        private readonly FileListing? imgListing;
        private readonly RBCONSubMetadata _metadata;

        public RBCONSubMetadata SharedMetadata => _metadata;

        public RBPackedCONMetadata(CONFile conFile, RBCONSubMetadata metadata, string nodeName, string location, string midiPath)
        {
            this.conFile = conFile;
            _metadata = metadata;

            if (midiPath == string.Empty)
                midiPath = location + ".mid";

            midiListing = conFile[midiPath];
            if (midiListing == null)
                throw new Exception($"Required midi file '{midiPath}' was not located");

            _metadata.Midi = new(midiPath, midiListing.lastWrite);
            string midiDirectory = conFile[midiListing.pathIndex].Filename;

            moggListing = conFile[location + ".mogg"];

            if (!location.StartsWith($"songs/{nodeName}"))
                nodeName = midiDirectory.Split('/')[1];

            string genPAth = $"songs/{nodeName}/gen/{nodeName}";
            miloListing = conFile[genPAth + ".milo_xbox"];
            imgListing = conFile[genPAth + "_keep.png_xbox"];
        }

        public YARGFile? LoadMidiFile()
        {
            if (midiListing == null)
                return null;
            return new YARGFile(conFile.LoadSubFile(midiListing));
        }

        public YARGFile? LoadMoggFile()
        {
            //if (Yarg_Mogg != null)
            //{
            //    // ReSharper disable once MustUseReturnValue
            //    return new YARGFile(YargMoggReadStream.DecryptMogg(Yarg_Mogg.FullName));
            //}

            if (_metadata.Mogg != null && File.Exists(_metadata.Mogg.FullName))
                return new YARGFile(_metadata.Mogg.FullName);

            if (moggListing != null)
                return new YARGFile(conFile.LoadSubFile(moggListing));
            return null;
        }

        public YARGFile? LoadMiloFile()
        {
            if (_metadata.Milo != null && File.Exists(_metadata.Milo.FullName))
                return new YARGFile(_metadata.Milo.FullName);

            if (miloListing != null)
                return new YARGFile(conFile.LoadSubFile(miloListing));

            return null;
        }

        public YARGFile? LoadImgFile()
        {
            if (_metadata.Image != null && File.Exists(_metadata.Image.FullName))
                return new YARGFile(_metadata.Image.FullName);

            if (imgListing != null)
                return new YARGFile(conFile.LoadSubFile(imgListing));

            return null;
        }

        public bool IsMoggUnencrypted()
        {
            //if (Yarg_Mogg != null)
            //{
            //    if (!File.Exists(Yarg_Mogg.FullName))
            //        throw new Exception("YARG Mogg file not present");
            //    return YargMoggReadStream.GetVersionNumber(Yarg_Mogg.FullName) == 0xF0;
            //}
            //else
            if (_metadata.Mogg != null && File.Exists(_metadata.Mogg.FullName))
            {
                using var fs = new FileStream(_metadata.Mogg.FullName, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[4];
                fs.Read(buffer);
                return BinaryPrimitives.ReadInt32LittleEndian(buffer) == 0x0A;
            }
            else if (miloListing != null)
                return conFile.GetMoggVersion(moggListing) == 0x0A;
            return false;
        }
    }
}
