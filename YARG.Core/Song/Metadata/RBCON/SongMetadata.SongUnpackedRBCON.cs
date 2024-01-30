using System;
using System.IO;
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
            private readonly AbridgedFileInfo? _dta;
            private readonly AbridgedFileInfo _midi;
            private readonly string _moggPath;
            private readonly string _miloPath;
            private readonly string _imgPath;

            public RBCONSubMetadata SharedMetadata { get; }

            public DateTime LastUpdateTime => _midi.LastUpdatedTime;

            public RBUnpackedCONMetadata(UnpackedCONGroup group, RBCONSubMetadata metadata, string nodename, string location)
            {
                _dta = group.DTA;
                SharedMetadata = metadata;

                if (!location.StartsWith($"songs/{nodename}"))
                    nodename = location.Split('/')[1];

                metadata.Directory = Path.Combine(group.Location, nodename);
                string midiPath = Path.Combine(metadata.Directory, $"{nodename}.mid");

                FileInfo midiInfo = new(midiPath);
                if (!midiInfo.Exists)
                    throw new Exception($"Required midi file '{midiPath}' was not located");

                _midi = new AbridgedFileInfo(midiInfo);
                _moggPath = Path.ChangeExtension(midiPath, null);

                string file = Path.Combine(metadata.Directory, "gen", nodename);
                _miloPath = file + ".milo_xbox";
                _imgPath = file + "_keep.png_xbox";
            }

            public static RBUnpackedCONMetadata? TryLoadFromCache(AbridgedFileInfo dta, YARGBinaryReader reader)
            {
                var midiInfo = AbridgedFileInfo.TryParseInfo(reader);
                if (midiInfo == null)
                {
                    return null;
                }

                AbridgedFileInfo? updateMidi = null;
                if (reader.ReadBoolean())
                {
                    updateMidi = AbridgedFileInfo.TryParseInfo(reader, false);
                    if (updateMidi == null)
                    {
                        return null;
                    }
                }

                var baseMetadata = new RBCONSubMetadata(updateMidi, reader);
                return new RBUnpackedCONMetadata(dta, midiInfo, baseMetadata);
            }

            public static RBUnpackedCONMetadata LoadFromCache_Quick(AbridgedFileInfo? dta, YARGBinaryReader reader)
            {
                var midiInfo = new AbridgedFileInfo(reader);
                var updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;
                var baseMetadata = new RBCONSubMetadata(updateMidi, reader);
                return new RBUnpackedCONMetadata(dta, midiInfo, baseMetadata);
            }

            public RBUnpackedCONMetadata(AbridgedFileInfo? dta, AbridgedFileInfo midi, RBCONSubMetadata baseMetadata)
            {
                _dta = dta;
                _midi = midi;

                _moggPath = Path.ChangeExtension(midi.FullName, null);

                string directory = Path.GetDirectoryName(midi.FullName);
                string nodename = Path.GetFileNameWithoutExtension(midi.FullName);

                string file = Path.Combine(directory, "gen", nodename);
                _miloPath = file + ".milo_xbox";
                _imgPath = file + "_keep.png_xbox";

                SharedMetadata = baseMetadata;
            }

            public void Serialize(BinaryWriter writer)
            {
                _midi.Serialize(writer);

                if (SharedMetadata.UpdateMidi != null)
                {
                    writer.Write(true);
                    SharedMetadata.UpdateMidi.Serialize(writer);
                }
                else
                {
                    writer.Write(false);
                }
                SharedMetadata.Serialize(writer);
            }

            public Stream? GetMidiStream()
            {
                if (_dta == null || !_dta.IsStillValid() || !_midi.IsStillValid())
                {
                    return null;
                }
                return new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            public byte[]? LoadMidiFile(CONFile? _)
            {
                if (_dta == null || !_dta.IsStillValid() || !_midi.IsStillValid())
                {
                    return null;
                }
                return File.ReadAllBytes(_midi.FullName);
            }

            public byte[]? LoadMiloFile()
            {
                if (SharedMetadata.Milo != null && SharedMetadata.Milo.Exists())
                {
                    return File.ReadAllBytes(SharedMetadata.Milo.FullName);
                }

                if (!File.Exists(_miloPath))
                {
                    return null;
                }
                return File.ReadAllBytes(_miloPath);
            }

            public byte[]? LoadImgFile()
            {
                if (SharedMetadata.Image != null && SharedMetadata.Image.Exists())
                {
                    return File.ReadAllBytes(SharedMetadata.Image.FullName);
                }

                if (!File.Exists(_imgPath))
                {
                    return null;
                }
                return File.ReadAllBytes(_imgPath);
            }

            public Stream? GetMoggStream()
            {
                var stream = SharedMetadata.GetMoggStream();
                if (stream != null)
                {
                    return stream;
                }

                string path = _moggPath + ".yarg_mogg";
                if (File.Exists(path))
                {
                    return new YargMoggReadStream(path);
                }

                path = _moggPath + ".mogg";
                if (!File.Exists(path))
                {
                    return null;
                }
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            }

            public bool IsMoggValid(CONFile? _)
            {
                using var stream = GetMoggStream();
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

        public static SongMetadata? UnpackedRBCONFromCache(AbridgedFileInfo dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var packedMeta = RBUnpackedCONMetadata.TryLoadFromCache(dta, reader);
            if (packedMeta == null)
            {
                return null;
            }

            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings);
        }

        public static SongMetadata UnpackedRBCONFromCache_Quick(AbridgedFileInfo? dta, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var packedMeta = RBUnpackedCONMetadata.LoadFromCache_Quick(dta, reader);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings);
        }
    }
}
