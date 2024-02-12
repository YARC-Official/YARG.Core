using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        [Serializable]
        public sealed class RBPackedCONMetadata : IRBCONMetadata
        {
            private readonly CONFileListing? _midiListing;
            private readonly CONFileListing? _moggListing;
            private readonly CONFileListing? _miloListing;
            private readonly CONFileListing? _imgListing;
            private readonly DateTime _lastMidiUpdate;
            
            public RBCONSubMetadata SharedMetadata { get; }

            public RBPackedCONMetadata(PackedCONGroup group, RBCONSubMetadata metadata, string nodeName, string location)
            {
                SharedMetadata = metadata;

                string midiPath = location + ".mid";
                _midiListing = group.CONFile.TryGetListing(midiPath);
                if (_midiListing == null)
                    throw new Exception($"Required midi file '{midiPath}' was not located");
                _lastMidiUpdate = _midiListing.lastWrite;

                _moggListing = group.CONFile.TryGetListing(location + ".mogg");
                
                if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = _midiListing.Filename.Split('/')[1];

                string genPath = $"songs/{nodeName}/gen/{nodeName}";
                _miloListing = group.CONFile.TryGetListing(genPath + ".milo_xbox");
                _imgListing = group.CONFile.TryGetListing(genPath + "_keep.png_xbox");

                string midiDirectory = group.CONFile.Listings[_midiListing.pathIndex].Filename;
                metadata.Directory = Path.Combine(group.Location, midiDirectory);
            }

            public static RBPackedCONMetadata? TryLoadFromCache(CONFile file, string nodename, BinaryReader reader)
            {
                string midiFilename = reader.ReadString();
                var midiListing = file.TryGetListing(midiFilename);
                if (midiListing == null)
                {
                    return null;
                }

                var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());
                if (midiListing.lastWrite != midiLastWrite)
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

                var moggListing = file.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"));

                if (!midiFilename.StartsWith($"songs/{nodename}"))
                    nodename = midiFilename.Split('/')[1];

                string genPath = $"songs/{nodename}/gen/{nodename}";
                var miloListing = file.TryGetListing(genPath + ".milo_xbox");
                var imgListing = file.TryGetListing(genPath + "_keep.png_xbox");

                var baseMetadata = new RBCONSubMetadata(updateMidi, reader);
                return new RBPackedCONMetadata(midiListing, midiLastWrite, moggListing, miloListing, imgListing, baseMetadata);
            }

            public static RBPackedCONMetadata LoadFromCache_Quick(CONFile file, string nodename, BinaryReader reader)
            {
                string midiFilename = reader.ReadString();
                var midiListing = file.TryGetListing(midiFilename);
                var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());

                var updateMidi = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

                var moggListing = file.TryGetListing(Path.ChangeExtension(midiFilename, ".mogg"));

                if (!midiFilename.StartsWith($"songs/{nodename}"))
                    nodename = midiFilename.Split('/')[1];

                string genPath = $"songs/{nodename}/gen/{nodename}";
                var miloListing = file.TryGetListing(genPath + ".milo_xbox");
                var imgListing = file.TryGetListing(genPath + "_keep.png_xbox");

                var baseMetadata = new RBCONSubMetadata(updateMidi, reader);
                return new RBPackedCONMetadata(midiListing, midiLastWrite, moggListing, miloListing, imgListing, baseMetadata);
            }

            private RBPackedCONMetadata(CONFileListing? midi, DateTime midiLastWrite, CONFileListing? moggListing, CONFileListing? miloListing, CONFileListing? imgListing, RBCONSubMetadata baseMetadata)
            {
                _midiListing = midi;
                _moggListing = moggListing;
                _miloListing = miloListing;
                _imgListing = imgListing;
                _lastMidiUpdate = midiLastWrite;

                SharedMetadata = baseMetadata;
            }

            public DateTime GetAbsoluteLastUpdateTime()
            {
                var lastUpdateTime = _midiListing?.ConFile.LastUpdatedTime ?? DateTime.MinValue;
                if (SharedMetadata.UpdateMidi != null)
                {
                    if (SharedMetadata.UpdateMidi.LastUpdatedTime > lastUpdateTime)
                    {
                        lastUpdateTime = SharedMetadata.UpdateMidi.LastUpdatedTime;
                    }
                }

                if (SharedMetadata.Upgrade != null)
                {
                    if (SharedMetadata.Upgrade.LastUpdatedTime > lastUpdateTime)
                    {
                        lastUpdateTime = SharedMetadata.Upgrade.LastUpdatedTime;
                    }
                }
                return lastUpdateTime;
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(_midiListing!.Filename);
                writer.Write(_midiListing.lastWrite.ToBinary());

                if (SharedMetadata.UpdateMidi != null)
                {
                    writer.Write(true);
                    SharedMetadata.UpdateMidi.Serialize(writer);
                }
                else
                    writer.Write(false);

                SharedMetadata.Serialize(writer);
            }

            public Stream? GetMidiStream()
            {
                if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiUpdate))
                    return null;
                return _midiListing.CreateStream();
            }

            public byte[]? LoadMidiFile(CONFile? file)
            {
                if (_midiListing == null || !_midiListing.IsStillValid(_lastMidiUpdate))
                {
                    return null;
                }
                return _midiListing.LoadAllBytes(file!);
            }

            public byte[]? LoadMiloFile()
            {
                if (SharedMetadata.Milo != null && SharedMetadata.Milo.Exists())
                {
                    return File.ReadAllBytes(SharedMetadata.Milo.FullName);
                }
                return _miloListing?.LoadAllBytes();
            }

            public byte[]? LoadImgFile()
            {
                if (SharedMetadata.Image != null && SharedMetadata.Image.Exists())
                {
                    return File.ReadAllBytes(SharedMetadata.Image.FullName);
                }
                return _imgListing?.LoadAllBytes();
            }

            public Stream? GetMoggStream()
            {
                var stream = SharedMetadata.GetMoggStream();
                if (stream != null)
                    return stream;
                return _moggListing?.CreateStream();
            }

            public bool IsMoggValid(CONFile? file)
            {
                using var stream = SharedMetadata.GetMoggStream();
                if (stream != null)
                {
                    int version = stream.Read<int>(Endianness.Little);
                    return version == 0x0A || version == 0xf0;
                }
                return _moggListing != null && CONFileListing.GetMoggVersion(_moggListing, file!) == 0x0A;
            }
        }

        private SongMetadata(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            RBCONSubMetadata rbMetadata = new();

            var dtaResults = ParseDTA(nodeName, rbMetadata, reader);
            _rbData = new RBPackedCONMetadata(group, rbMetadata, nodeName, dtaResults.location);
            _directory = rbMetadata.Directory;

            ApplyRBCONUpdates(nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);
            FinalizeRBCONAudioValues(rbMetadata, dtaResults.pans, dtaResults.volumes, dtaResults.cores);
        }

        public static (ScanResult, SongMetadata?) FromPackedRBCON(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<SongUpdate>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                SongMetadata song = new(group, nodeName, reader, updates, upgrades);
                var result = song.ParseRBCONMidi(group.CONFile);
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

        public static SongMetadata? PackedRBCONFromCache(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var packedMeta = RBPackedCONMetadata.TryLoadFromCache(file, nodename, reader);
            if (packedMeta == null)
            {
                return null;
            }

            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings)
            {
                _directory = packedMeta.SharedMetadata.Directory
            };
        }

        public static SongMetadata PackedRBCONFromCache_Quick(CONFile file, string nodename, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, BinaryReader reader, CategoryCacheStrings strings)
        {
            var packedMeta = RBPackedCONMetadata.LoadFromCache_Quick(file, nodename, reader);
            if (upgrades.TryGetValue(nodename, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings)
            {
                _directory = packedMeta.SharedMetadata.Directory
            };
        }
    }
}
