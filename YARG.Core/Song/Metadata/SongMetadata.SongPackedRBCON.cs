using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        [Serializable]
        public sealed class RBPackedCONMetadata : IRBCONMetadata
        {
            public readonly CONFile conFile;
            public readonly FileListing? midiListing;
            public readonly FileListing? moggListing;
            public readonly FileListing? miloListing;
            public readonly FileListing? imgListing;
            public readonly RBCONSubMetadata _metadata;
            public readonly DateTime _midiLastWrite;

            public RBCONSubMetadata SharedMetadata => _metadata;

            public DateTime MidiLastWrite => _midiLastWrite;

            public RBPackedCONMetadata(CONFile conFile, RBCONSubMetadata metadata, string nodeName, string location, string midiPath)
            {
                this.conFile = conFile;
                _metadata = metadata;

                if (midiPath == string.Empty)
                    midiPath = location + ".mid";

                midiListing = conFile.TryGetListing(midiPath);
                if (midiListing == null)
                    throw new Exception($"Required midi file '{midiPath}' was not located");
                _midiLastWrite = midiListing.lastWrite;

                string midiDirectory = conFile[midiListing.pathIndex].Filename;

                moggListing = conFile.TryGetListing(location + ".mogg");

                if (!location.StartsWith($"songs/{nodeName}"))
                    nodeName = midiDirectory.Split('/')[1];

                string genPAth = $"songs/{nodeName}/gen/{nodeName}";
                miloListing = conFile.TryGetListing(genPAth + ".milo_xbox");
                imgListing = conFile.TryGetListing(genPAth + "_keep.png_xbox");

                metadata.Directory = Path.Combine(conFile.filename, midiDirectory);
            }

            public RBPackedCONMetadata(CONFile file, string nodeName, FileListing? midi, DateTime midiLastWrite, FileListing? moggListing, AbridgedFileInfo? moggInfo, AbridgedFileInfo? updateInfo, YARGBinaryReader reader)
            {
                conFile = file;
                midiListing = midi;
                _midiLastWrite = midiLastWrite;

                if (moggListing != null)
                    this.moggListing = moggListing;

                if (midiListing != null && !midiListing.Filename.StartsWith($"songs/{nodeName}"))
                    nodeName = conFile[midiListing.pathIndex].Filename.Split('/')[1];

                string genPAth = $"songs/{nodeName}/gen/{nodeName}";

                AbridgedFileInfo? miloInfo = null;
                if (reader.ReadBoolean())
                    miloListing = conFile.TryGetListing(genPAth + ".milo_xbox");
                else
                {
                    string milopath = reader.ReadLEBString();
                    if (milopath != string.Empty)
                    {
                        FileInfo info = new(milopath);
                        if (info.Exists)
                            miloInfo = info;
                    }
                }

                AbridgedFileInfo? imageInfo = null;
                if (reader.ReadBoolean())
                    imgListing = conFile.TryGetListing(genPAth + "_keep.png_xbox");
                else
                {
                    string imgpath = reader.ReadLEBString();
                    if (imgpath != string.Empty)
                    {
                        FileInfo info = new(imgpath);
                        if (info.Exists)
                            imageInfo = info;
                    }
                }

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
                    writer.Write(_metadata.Mogg.FullName);
                    writer.Write(_metadata.Mogg.LastWriteTime.ToBinary());
                }

                if (_metadata.UpdateMidi != null)
                {
                    writer.Write(true);
                    writer.Write(_metadata.UpdateMidi.FullName);
                    writer.Write(_metadata.UpdateMidi.LastWriteTime.ToBinary());
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

            public byte[]? LoadMidiFile()
            {
                if (midiListing == null)
                    return null;
                return conFile.LoadSubFile(midiListing);
            }

            public byte[]? LoadMoggFile()
            {
                //if (Yarg_Mogg != null)
                //{
                //    // ReSharper disable once MustUseReturnValue
                //    return new YARGFile(YargMoggReadStream.DecryptMogg(Yarg_Mogg.FullName));
                //}

                if (_metadata.Mogg != null && File.Exists(_metadata.Mogg.FullName))
                    return File.ReadAllBytes(_metadata.Mogg.FullName);

                if (moggListing != null)
                    return conFile.LoadSubFile(moggListing);
                return null;
            }

            public byte[]? LoadMiloFile()
            {
                if (_metadata.Milo != null && File.Exists(_metadata.Milo.FullName))
                    return File.ReadAllBytes(_metadata.Milo.FullName);

                if (miloListing != null)
                    return conFile.LoadSubFile(miloListing);

                return null;
            }

            public byte[]? LoadImgFile()
            {
                if (_metadata.Image != null && File.Exists(_metadata.Image.FullName))
                    return File.ReadAllBytes(_metadata.Image.FullName);

                if (imgListing != null)
                    return conFile.LoadSubFile(imgListing);

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
                else if (moggListing != null)
                    return conFile.GetMoggVersion(moggListing) == 0x0A;
                return false;
            }
        }

        private SongMetadata(CONFile conFile, string nodeName, YARGDTAReader reader)
        {
            RBCONSubMetadata rbMetadata = new();

            var dtaResults = ParseDTA(nodeName, rbMetadata, reader);
            _rbData = new RBPackedCONMetadata(conFile, rbMetadata, nodeName, dtaResults.location, dtaResults.midiPath);
            _directory = rbMetadata.Directory;
        }

        public static (ScanResult, SongMetadata?) FromPackedRBCON(CONFile conFile, string nodeName, YARGDTAReader reader, Dictionary<string, List<(string, YARGDTAReader)>> updates, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades)
        {
            try
            {
                SongMetadata song = new(conFile, nodeName, reader);
                song.ApplyRBCONUpdates(nodeName, updates);
                song.ApplyRBProUpgrade(nodeName, upgrades);

                var result = song.ParseRBCONMidi();
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

        public static SongMetadata? PackedRBCONFromCache(CONFile conFile, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var midiListing = conFile.TryGetListing(reader.ReadLEBString());
            var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (midiListing == null || midiListing.lastWrite != midiLastWrite)
                return null;

            FileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = conFile.TryGetListing(reader.ReadLEBString());
                if (moggListing == null || moggListing.lastWrite != DateTime.FromBinary(reader.ReadInt64()))
                    return null;
            }
            else
            {
                FileInfo info = new(reader.ReadLEBString());
                if (!info.Exists || info.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return null;
                moggInfo = info;
            }

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                FileInfo info = new(reader.ReadLEBString());
                if (!info.Exists || info.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return null;
                updateInfo = info;
            }

            RBPackedCONMetadata packedMeta = new(conFile, nodeName, midiListing, midiLastWrite, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            return new SongMetadata(packedMeta, reader, strings);
        }

        public static SongMetadata PackedRBCONFromCache_Quick(CONFile conFile, string nodeName, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var midiListing = conFile.TryGetListing(reader.ReadLEBString());
            var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());

            FileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = conFile.TryGetListing(reader.ReadLEBString());
                reader.Position += 8;
            }
            else
            {
                string moggName = reader.ReadLEBString();
                var moggTime = DateTime.FromBinary(reader.ReadInt64());
                moggInfo = new(moggName, moggTime);
            }

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                string updateName = reader.ReadLEBString();
                var updateTime = DateTime.FromBinary(reader.ReadInt64());
                updateInfo = new(updateName, updateTime);
            }

            RBPackedCONMetadata packedMeta = new(conFile, nodeName, midiListing, midiLastWrite, moggListing, moggInfo, updateInfo, reader);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                packedMeta.SharedMetadata.Upgrade = upgrade.Item2;
            return new SongMetadata(packedMeta, reader, strings);
        }
    }
}
