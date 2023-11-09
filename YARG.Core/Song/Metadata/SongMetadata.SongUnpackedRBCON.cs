using System;
using System.IO;
using System.Buffers.Binary;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        [Serializable]
        public sealed class RBUnpackedCONMetadata : IRBCONMetadata
        {
            private readonly AbridgedFileInfo? DTA;
            private readonly RBCONSubMetadata _metadata;
            private readonly AbridgedFileInfo Midi;

            public RBCONSubMetadata SharedMetadata => _metadata;

            public DateTime MidiLastWrite => Midi.LastWriteTime;

            public RBUnpackedCONMetadata(string folder, AbridgedFileInfo dta, RBCONSubMetadata metadata, string nodeName, string location)
            {
                _metadata = metadata;
                DTA = dta;

                if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = location.Split('/')[1];

                folder = Path.Combine(folder, nodeName);
                string file = Path.Combine(folder, nodeName);
                string midiPath = file + ".mid";

                FileInfo midiInfo = new(midiPath);
                if (!midiInfo.Exists)
                    throw new Exception($"Required midi file '{midiPath}' was not located");

                Midi = midiInfo;
                metadata.Directory = folder;

                FileInfo mogg = new(file + ".yarg_mogg");
                metadata.Mogg = mogg.Exists ? mogg : new AbridgedFileInfo(file + ".mogg");

                file = Path.Combine(folder, "gen", nodeName);
                metadata.Milo = new(file + ".milo_xbox");
                metadata.Image = new(file + "_keep.png_xbox");
            }

            public RBUnpackedCONMetadata(AbridgedFileInfo? dta, AbridgedFileInfo midi, AbridgedFileInfo? moggInfo, AbridgedFileInfo? updateInfo, YARGBinaryReader reader)
            {
                DTA = dta;
                Midi = midi;

                string str = reader.ReadLEBString();
                AbridgedFileInfo? miloInfo = str.Length > 0 ? new(str) : null;

                str = reader.ReadLEBString();
                AbridgedFileInfo? imageInfo = str.Length > 0 ? new(str) : null;

                _metadata = new(reader)
                {
                    Mogg = moggInfo,
                    UpdateMidi = updateInfo,
                    Milo = miloInfo,
                    Image = imageInfo,
                };
            }

            public void Serialize(BinaryWriter writer)
            {
                Midi.Serialize(writer);
                _metadata.Mogg!.Serialize(writer);

                if (_metadata.UpdateMidi != null)
                {
                    writer.Write(true);
                    _metadata.UpdateMidi.Serialize(writer);
                }
                else
                    writer.Write(false);

                if (_metadata.Milo != null)
                    writer.Write(_metadata.Milo.FullName);
                else
                    writer.Write(string.Empty);

                if (_metadata.Image != null)
                    writer.Write(_metadata.Image.FullName);
                else
                    writer.Write(string.Empty);

                _metadata.Serialize(writer);
            }

            public Stream? GetMidiStream()
            {
                if (DTA == null || !DTA.IsStillValid() || !Midi.IsStillValid())
                    return null;
                return new FileStream(Midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            public byte[]? LoadMidiFile(CONFile? _)
            {
                if (DTA == null || !DTA.IsStillValid() || !Midi.IsStillValid())
                    return null;
                return File.ReadAllBytes(Midi.FullName);
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

            public Stream? GetMoggStream()
            {
                return _metadata.GetMoggStream();
            }

            public bool IsMoggValid(CONFile? _)
            {
                using var stream = _metadata.GetMoggStream();
                if (stream == null)
                    return false;

                int version = stream.ReadInt32LE();
                return version == 0x0A || version == 0xf0;
            }
        }

        private SongMetadata(string folder, AbridgedFileInfo dta, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            RBCONSubMetadata rbMetadata = new();

            var dtaResults = ParseDTA(nodeName, rbMetadata, reader);
            _rbData = new RBUnpackedCONMetadata(folder, dta, rbMetadata, nodeName, dtaResults.location);
            _directory = rbMetadata.Directory;

            if (_playlist.Length == 0)
                _playlist = Path.GetFileName(folder);

            ApplyRBCONUpdates(nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);
            FinalizeRBCONAudioValues(rbMetadata, dtaResults.pans, dtaResults.volumes, dtaResults.cores);
        }

        public static (ScanResult, SongMetadata?) FromUnpackedRBCON(string folder, AbridgedFileInfo dta, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                SongMetadata song = new(folder, dta, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(null);
                if (result != ScanResult.Success)
                    return (result, null);
                return (result, song);
            }
            catch (Exception ex)
            {
                YargTrace.LogError(ex.Message);
                return (ScanResult.DTAError, null);
            }
        }

        public static SongMetadata? UnpackedRBCONFromCache(AbridgedFileInfo dta, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var midiInfo = AbridgedFileInfo.TryParseInfo(reader);
            if (midiInfo == null)
                return null;

            var moggInfo = AbridgedFileInfo.TryParseInfo(reader);
            if (moggInfo == null)
                return null;

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = AbridgedFileInfo.TryParseInfo(reader);
                if (updateInfo == null)
                    return null;
            }

            RBUnpackedCONMetadata packedMeta = new(dta, midiInfo, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            return new SongMetadata(packedMeta, reader, strings);
        }

        public static SongMetadata UnpackedRBCONFromCache_Quick(AbridgedFileInfo? dta, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            AbridgedFileInfo midiInfo = new(reader);
            AbridgedFileInfo moggInfo = new(reader);
            var updateInfo = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

            RBUnpackedCONMetadata packedMeta = new(dta, midiInfo, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            return new SongMetadata(packedMeta, reader, strings);
        }
    }
}
