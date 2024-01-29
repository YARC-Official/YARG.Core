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

            public DateTime MidiLastUpdateTime => Midi.LastUpdatedTime;

            public RBUnpackedCONMetadata(UnpackedCONGroup group, RBCONSubMetadata metadata, string nodeName, string location)
            {
                _metadata = metadata;
                DTA = group.DTA;

                if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = location.Split('/')[1];

                string folder = Path.Combine(group.Location, nodeName);
                string file = Path.Combine(folder, nodeName);
                string midiPath = file + ".mid";

                FileInfo midiInfo = new(midiPath);
                if (!midiInfo.Exists)
                    throw new Exception($"Required midi file '{midiPath}' was not located");

                Midi = new AbridgedFileInfo(midiInfo);
                metadata.Directory = folder;

                FileInfo mogg = new(file + ".yarg_mogg");
                if (mogg.Exists)
                {
                    metadata.Mogg = new AbridgedFileInfo(mogg);
                }
                else
                {
                    metadata.Mogg = new AbridgedFileInfo(file + ".mogg");
                }

                file = Path.Combine(folder, "gen", nodeName);
                metadata.Milo = new(file + ".milo_xbox");
                metadata.Image = new(file + "_keep.png_xbox");
            }

            public RBUnpackedCONMetadata(AbridgedFileInfo? dta, AbridgedFileInfo midi, AbridgedFileInfo? moggInfo, AbridgedFileInfo? updateInfo, YARGBinaryReader reader)
            {
                DTA = dta;
                Midi = midi;

                string miloname = reader.ReadLEBString();
                var miloInfo = miloname.Length > 0 ? new AbridgedFileInfo(miloname) : null;

                string imagename = reader.ReadLEBString();
                var imageInfo = imagename.Length > 0 ? new AbridgedFileInfo(imagename) : null;

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
                {
                    writer.Write(false);
                }

                writer.Write(_metadata.Milo?.FullName ?? string.Empty);
                writer.Write(_metadata.Image?.FullName ?? string.Empty);
                _metadata.Serialize(writer);
            }

            public Stream? GetMidiStream()
            {
                if (DTA == null || !DTA.IsStillValid() || !Midi.IsStillValid())
                {
                    return null;
                }
                return new FileStream(Midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            public byte[]? LoadMidiFile(CONFile? _)
            {
                if (DTA == null || !DTA.IsStillValid() || !Midi.IsStillValid())
                {
                    return null;
                }
                return File.ReadAllBytes(Midi.FullName);
            }

            public byte[]? LoadMiloFile()
            {
                if (_metadata.Milo == null || !File.Exists(_metadata.Milo.FullName))
                {
                    return null;
                }
                return File.ReadAllBytes(_metadata.Milo.FullName);
            }

            public byte[]? LoadImgFile()
            {
                if (_metadata.Image == null || !File.Exists(_metadata.Image.FullName))
                {
                    return null;
                }
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
                {
                    return false;
                }

                int version = stream.Read<int>(Endianness.Little);
                return version == 0x0A || version == 0xf0;
            }
        }

        private SongMetadata(UnpackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            RBCONSubMetadata rbMetadata = new();

            var dtaResults = ParseDTA(nodeName, rbMetadata, reader);
            _rbData = new RBUnpackedCONMetadata(group, rbMetadata, nodeName, dtaResults.location);
            _directory = rbMetadata.Directory;

            ApplyRBCONUpdates(nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);
            FinalizeRBCONAudioValues(rbMetadata, dtaResults.pans, dtaResults.volumes, dtaResults.cores);
        }

        public static (ScanResult, SongMetadata?) FromUnpackedRBCON(UnpackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                SongMetadata song = new(group, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(null);
                if (result != ScanResult.Success)
                    return (result, null);

                if (song._playlist.Length == 0)
                    song._playlist = group.DefaultPlaylist;

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
