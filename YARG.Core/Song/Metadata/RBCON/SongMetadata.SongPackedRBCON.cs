using System;
using System.Buffers.Binary;
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
            public readonly CONFileListing? midiListing;
            public readonly CONFileListing? moggListing;
            public readonly CONFileListing? miloListing;
            public readonly CONFileListing? imgListing;
            public readonly RBCONSubMetadata _metadata;
            public readonly DateTime _midiLastWrite;

            public RBCONSubMetadata SharedMetadata => _metadata;

            public DateTime MidiLastUpdateTime => _midiLastWrite;

            public RBPackedCONMetadata(PackedCONGroup group, RBCONSubMetadata metadata, string nodeName, string location)
            {
                _metadata = metadata;

                string midiPath = location + ".mid";
                midiListing = group.CONFile.TryGetListing(midiPath);
                if (midiListing == null)
                    throw new Exception($"Required midi file '{midiPath}' was not located");
                _midiLastWrite = midiListing.lastWrite;

                string midiDirectory = group.CONFile.Listings[midiListing.pathIndex].Filename;

                moggListing = group.CONFile.TryGetListing(location + ".mogg");

                if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = midiDirectory.Split('/')[1];

                string genPAth = $"songs/{nodeName}/gen/{nodeName}";
                miloListing = group.CONFile.TryGetListing(genPAth + ".milo_xbox");
                imgListing = group.CONFile.TryGetListing(genPAth + "_keep.png_xbox");

                metadata.Directory = Path.Combine(group.Location, midiDirectory);
            }

            public RBPackedCONMetadata(CONFile file, string nodeName, CONFileListing? midi, DateTime midiLastWrite, CONFileListing? moggListing, AbridgedFileInfo? moggInfo, AbridgedFileInfo? updateInfo, YARGBinaryReader reader)
            {
                midiListing = midi;
                _midiLastWrite = midiLastWrite;

                if (moggListing != null)
                    this.moggListing = moggListing;

                if (midiListing != null && !midiListing.Filename.StartsWith($"songs/{nodeName}"))
                    nodeName = file.Listings[midiListing.pathIndex].Filename.Split('/')[1];

                string genPAth = $"songs/{nodeName}/gen/{nodeName}";

                AbridgedFileInfo? miloInfo = null;
                if (reader.ReadBoolean())
                {
                    miloListing = file.TryGetListing(genPAth + ".milo_xbox");
                }
                else
                {
                    string milopath = reader.ReadLEBString();
                    if (milopath.Length > 0)
                    {
                        var info = new FileInfo(milopath);
                        if (info.Exists)
                        {
                            miloInfo = new AbridgedFileInfo(info);
                        }
                    }
                }

                AbridgedFileInfo? imageInfo = null;
                if (reader.ReadBoolean())
                {
                    imgListing = file.TryGetListing(genPAth + "_keep.png_xbox");
                }
                else
                {
                    string imgpath = reader.ReadLEBString();
                    if (imgpath != string.Empty)
                    {
                        var info = new FileInfo(imgpath);
                        if (info.Exists)
                        {
                            imageInfo = new AbridgedFileInfo(info);
                        }
                    }
                }

                _metadata = new RBCONSubMetadata(reader)
                {
                    Mogg = moggInfo,
                    UpdateMidi = updateInfo,
                    Milo = miloInfo,
                    Image = imageInfo,
                };
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(midiListing!.Filename);
                writer.Write(_midiLastWrite.ToBinary());

                if (_metadata.Mogg == null)
                {
                    writer.Write(true);
                    writer.Write(moggListing!.Filename);
                    writer.Write(moggListing.lastWrite.ToBinary());
                }
                else
                {
                    writer.Write(false);
                    _metadata.Mogg.Serialize(writer);
                }

                if (_metadata.UpdateMidi != null)
                {
                    writer.Write(true);
                    _metadata.UpdateMidi.Serialize(writer);
                }
                else
                    writer.Write(false);

                if (_metadata.Milo != null)
                {
                    writer.Write(false);
                    writer.Write(_metadata.Milo.FullName);
                }
                else
                    writer.Write(true);

                if (_metadata.Image != null)
                {
                    writer.Write(false);
                    writer.Write(_metadata.Image.FullName);
                }
                else
                    writer.Write(true);

                _metadata.Serialize(writer);
            }

            public Stream? GetMidiStream()
            {
                if (midiListing == null || !midiListing.IsStillValid())
                    return null;
                return midiListing.CreateStream();
            }

            public byte[]? LoadMidiFile(CONFile? file)
            {
                if (midiListing == null || !midiListing.IsStillValid())
                    return null;
                return midiListing.LoadAllBytes(file!);
            }

            public byte[]? LoadMiloFile()
            {
                if (_metadata.Milo != null && File.Exists(_metadata.Milo.FullName))
                    return File.ReadAllBytes(_metadata.Milo.FullName);
                return miloListing?.LoadAllBytes();
            }

            public byte[]? LoadImgFile()
            {
                if (_metadata.Image != null && File.Exists(_metadata.Image.FullName))
                    return File.ReadAllBytes(_metadata.Image.FullName);
                return imgListing?.LoadAllBytes();
            }

            public Stream? GetMoggStream()
            {
                var stream = _metadata.GetMoggStream();
                if (stream != null)
                    return stream;
                return moggListing?.CreateStream();
            }

            public bool IsMoggValid(CONFile? file)
            {
                using var stream = _metadata.GetMoggStream();
                if (stream != null)
                {
                    int version = stream.Read<int>(Endianness.Little);
                    return version == 0x0A || version == 0xf0;
                }
                else if (moggListing != null)
                    return CONFileListing.GetMoggVersion(moggListing, file!) == 0x0A;
                return false;
            }
        }

        private SongMetadata(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            RBCONSubMetadata rbMetadata = new();

            var dtaResults = ParseDTA(nodeName, rbMetadata, reader);
            _rbData = new RBPackedCONMetadata(group, rbMetadata, nodeName, dtaResults.location);
            _directory = rbMetadata.Directory;

            ApplyRBCONUpdates(nodeName, updates);
            ApplyRBProUpgrade(nodeName, upgrades);
            FinalizeRBCONAudioValues(rbMetadata, dtaResults.pans, dtaResults.volumes, dtaResults.cores);
        }

        public static (ScanResult, SongMetadata?) FromPackedRBCON(PackedCONGroup group, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
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

        public static SongMetadata? PackedRBCONFromCache(CONFile file, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var midiListing = file.TryGetListing(reader.ReadLEBString());
            if (midiListing == null)
            {
                return null;
            }

            var midiLastWrite = DateTime.FromBinary(reader.Read<long>(Endianness.Little));
            if (midiListing.lastWrite != midiLastWrite)
            {
                return null;
            }

            CONFileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file.TryGetListing(reader.ReadLEBString());
                if (moggListing == null || moggListing.lastWrite != DateTime.FromBinary(reader.Read<long>(Endianness.Little)))
                {
                    return null;
                }
            }
            else
            {
                moggInfo = AbridgedFileInfo.TryParseInfo(reader);
                if (moggInfo == null)
                {
                    return null;
                }
            }

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = AbridgedFileInfo.TryParseInfo(reader);
                if (updateInfo == null)
                {
                    return null;
                }
            }

            RBPackedCONMetadata packedMeta = new(file, nodeName, midiListing, midiLastWrite, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings);
        }

        public static SongMetadata PackedRBCONFromCache_Quick(CONFile file, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var midiListing = file.TryGetListing(reader.ReadLEBString());
            var midiLastWrite = DateTime.FromBinary(reader.Read<long>(Endianness.Little));

            CONFileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file.TryGetListing(reader.ReadLEBString());
                reader.Move(SIZEOF_DATETIME);
            }
            else
            {
                moggInfo = new AbridgedFileInfo(reader);
            }

            var updateInfo = reader.ReadBoolean() ? new AbridgedFileInfo(reader) : null;

            RBPackedCONMetadata packedMeta = new(file, nodeName, midiListing, midiLastWrite, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
            {
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            }
            return new SongMetadata(packedMeta, reader, strings);
        }
    }
}
