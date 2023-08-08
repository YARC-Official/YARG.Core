using System;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Song.Deserialization.Ini;
using YARG.Core.Song.Deserialization;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace YARG.Core.Song
{
    public sealed class RBUnpackedCONMetadata : IRBCONMetadata
    {
        private readonly AbridgedFileInfo? DTA;
        private readonly RBCONSubMetadata _metadata;
        private readonly AbridgedFileInfo? Yarg_Mogg = null;

        public RBCONSubMetadata SharedMetadata => _metadata;

        public RBUnpackedCONMetadata(string folder, AbridgedFileInfo dta, RBCONSubMetadata metadata, string nodeName, string location, string midiPath)
        {
            this._metadata = metadata;
            DTA = dta;
            string file = Path.Combine(folder, location);

            if (midiPath == string.Empty)
                midiPath = file + ".mid";

            FileInfo midiInfo = new(midiPath);
            if (!midiInfo.Exists)
                throw new Exception($"Required midi file '{midiPath}' was not located");
            metadata.Midi = midiInfo;

            //FileInfo mogg = new(file + ".yarg_mogg");
            //if (mogg.Exists)
            //    Yarg_Mogg = mogg;
            //else
            metadata.Mogg = new(file + ".mogg");

            if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = location.Split('/')[1];

            file = Path.Combine(folder, $"songs/{nodeName}/gen/{nodeName}");
            metadata.Milo = new(file + ".milo_xbox");
            metadata.Image = new(file + "_keep.png_xbox");
        }

        public byte[]? LoadMidiFile()
        {
            if (!Midi.IsStillValid())
                return null;
            return File.ReadAllBytes(Midi.FullName);
        }

        public byte[]? LoadMoggFile()
        {
            //if (Yarg_Mogg != null)
            //{
            //    // ReSharper disable once MustUseReturnValue
            //    return new YARGFile(YargMoggReadStream.DecryptMogg(Yarg_Mogg.FullName));
            //}

            if (_metadata.Mogg == null || !File.Exists(_metadata.Mogg.FullName))
                return null;
            return File.ReadAllBytes(_metadata.Mogg.FullName);
        }

        public byte[]? LoadMiloFile()
        {
            if (_metadata.Milo == null || !File.Exists(_metadata.Milo.FullName))
                return null;
            return File.ReadAllBytes(_metadata.Milo.FullName);
        }

        public byte[]? LoadImgFile()
        {
            if (_metadata.Image == null || !File.Exists(_metadata.Image.FullName))
                return null;
            return File.ReadAllBytes(_metadata.Image.FullName);
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
            if (_metadata.Mogg == null || !File.Exists(_metadata.Mogg.FullName))
                return false;

            using var fs = new FileStream(_metadata.Mogg.FullName, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4];
            fs.Read(buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer) == 0x0A;
        }
    }

    public sealed partial class SongMetadata
    {
        public static (ScanResult, SongMetadata?) FromUnpackedRBCON(string folder, AbridgedFileInfo dta, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            SongMetadata metadata = new();
            RBCONSubMetadata rbMetadata = new();

            try
            {
                var dtaResults = metadata.ParseDTA(nodeName, rbMetadata, reader);
                metadata._rbData = new RBUnpackedCONMetadata(folder, dta, rbMetadata, nodeName, dtaResults.location, dtaResults.midiPath);
                var scanResult = metadata.ProcessRBCON(nodeName, updates, upgrades);
                return (scanResult, metadata);
            }
            catch (Exception e)
            {
                return (ScanResult.DTAError, null);
                //AddToBadSongs(node.Key + $" - Node {name}", ScanResult.DTAError);
                //AddErrors(e);
            }
        }
    }
    
}
