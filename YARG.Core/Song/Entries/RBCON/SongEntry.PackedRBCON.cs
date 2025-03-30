using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Venue;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    internal sealed class PackedRBCONEntry : RBCONEntry
    {
        private CONFileListing? _midiListing;
        private CONFileListing? _moggListing;
        private CONFileListing? _miloListing;
        private CONFileListing? _imgListing;
        private string          _psuedoDirectory;

        public override EntryType SubType => EntryType.CON;
        public override string SortBasedLocation => _psuedoDirectory;
        public override string ActualLocation => _root.FullName;
        protected override DateTime MidiLastWriteTime => _root.LastWriteTime;

        internal override void Serialize(MemoryStream stream, CacheWriteIndices node)
        {
            stream.Write(_subName);
            base.Serialize(stream, node);
        }

        public override YARGImage LoadAlbumData()
        {
            var image = LoadUpdateAlbumData();
            if (!image.IsAllocated && _imgListing != null)
            {
                var bytes = CONFileStream.LoadFile(_root.FullName, _imgListing);
                image = YARGImage.TransferDXT(ref bytes);
            }
            return image;
        }

        public override BackgroundResult? LoadBackground()
        {
            if (_midiListing == null)
            {
                return null;
            }

            string actualDirectory = Path.GetDirectoryName(_root.FullName)!;
            string conName = Path.GetFileNameWithoutExtension(_root.FullName);
            string specifcVenue = Path.Combine(actualDirectory, _subName + YARGROUND_EXTENSION);
            if (File.Exists(specifcVenue))
            {
                var stream = File.OpenRead(specifcVenue);
                return new BackgroundResult(BackgroundType.Yarground, stream);
            }

            specifcVenue = Path.Combine(actualDirectory, conName + YARGROUND_EXTENSION);
            if (File.Exists(specifcVenue))
            {
                var stream = File.OpenRead(specifcVenue);
                return new BackgroundResult(BackgroundType.Yarground, stream);
            }

            var venues = Directory.GetFiles(actualDirectory, YARGROUND_EXTENSION);
            if (venues.Length > 0)
            {
                var stream = File.OpenRead(venues[BACKROUND_RNG.Next(venues.Length)]);
                return new BackgroundResult(BackgroundType.Yarground, stream);
            }

            foreach (var name in new[]{ _subName, conName, "bg", "background", "video" })
            {
                string fileBase = Path.Combine(actualDirectory, name);
                foreach (var ext in VIDEO_EXTENSIONS)
                {
                    string backgroundPath = fileBase + ext;
                    if (File.Exists(backgroundPath))
                    {
                        var stream = File.OpenRead(backgroundPath);
                        return new BackgroundResult(BackgroundType.Video, stream);
                    }
                }
            }

            foreach (var name in new[]{ _subName, conName, "bg", "background" })
            {
                var fileBase = Path.Combine(actualDirectory, name);
                foreach (var ext in IMAGE_EXTENSIONS)
                {
                    string backgroundPath = fileBase + ext;
                    if (File.Exists(backgroundPath))
                    {
                        var image = YARGImage.Load(backgroundPath);
                        if (image.IsAllocated)
                        {
                            return new BackgroundResult(image);
                        }
                    }
                }
            }
            return null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
            var data = LoadUpdateMiloData();
            if (!data.IsAllocated && _miloListing != null)
            {
                data = CONFileStream.LoadFile(_root.FullName, _miloListing);
            }
            return data;
        }

        protected override FixedArray<byte> GetMainMidiData()
        {
            return _midiListing != null
                ? CONFileStream.LoadFile(_root.FullName, _midiListing)
                : FixedArray<byte>.Null;
        }

        protected override Stream? GetMoggStream()
        {
            var stream = LoadUpdateMoggStream();
            if (stream == null && _moggListing != null)
            {
                stream = CONFileStream.CreateStream(_root.FullName, _moggListing);
            }
            return stream;
        }

        private PackedRBCONEntry(in AbridgedFileInfo root, string nodeName)
            : base(in root, nodeName)
        {
            _midiListing = null!;
            _psuedoDirectory = string.Empty;
        }

        public static ScanExpected<RBCONEntry> Create(in RBScanParameters parameters, List<CONFileListing> listings, Stream stream)
        {
            try
            {
                var entry = new PackedRBCONEntry(in parameters.Root, parameters.NodeName)
                {
                    _updateDirectoryAndDtaLastWrite = parameters.UpdateDirectory,
                    _updateMidiLastWrite = parameters.UpdateMidi,
                    _upgrade = parameters.Upgrade
                };
                entry._metadata.Playlist = parameters.DefaultPlaylist;

                var location = ProcessDTAs(entry, parameters.BaseDta, parameters.UpdateDta, parameters.UpgradeDta);
                if (!location)
                {
                    return new ScanUnexpected(location.Error);
                }

                if (!listings.FindListing(location.Value + ".mid", out entry._midiListing))
                {
                    return new ScanUnexpected(ScanResult.MissingCONMidi);
                }

                if (!listings.FindListing(location.Value + ".mogg", out entry._moggListing))
                {
                    return new ScanUnexpected(ScanResult.MoggError);
                }

                var mainMidi = FixedArray<byte>.Null;
                long moggLocation = CONFileStream.CalculateBlockLocation(entry._moggListing.BlockOffset, entry._moggListing.Shift);
                lock (stream)
                {
                    if (stream.Seek(moggLocation, SeekOrigin.Begin) != moggLocation || stream.Read<int>(Endianness.Little) != UNENCRYPTED_MOGG)
                    {
                        return new ScanUnexpected(ScanResult.MoggError);
                    }
                    mainMidi = CONFileStream.LoadFile(stream, entry._midiListing);
                }

                var result = ScanMidis(entry, in mainMidi);
                mainMidi.Dispose();
                if (result != ScanResult.Success)
                {
                    return new ScanUnexpected(result);
                }
                entry._psuedoDirectory = Path.Combine(parameters.Root.FullName, listings[entry._midiListing.PathIndex].Name);
                entry._subName = location.Value[6..location.Value.IndexOf('/', 6)];

                string genPath = $"songs/{entry._subName}/gen/{entry._subName}";
                listings.FindListing(genPath + ".milo_xbox", out entry._miloListing);
                listings.FindListing(genPath + "_keep.png_xbox", out entry._imgListing);
                entry.SetSortStrings();
                return entry;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                return new ScanUnexpected(ScanResult.DTAError);
            }
        }

        public static PackedRBCONEntry? TryDeserialize(List<CONFileListing> listings, in AbridgedFileInfo conInfo, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string subname = stream.ReadString();
            string location = $"songs/{subname}/{subname}";
            if (!listings.FindListing(location + ".mid", out var midiListing))
            {
                return null;
            }

            if (!listings.FindListing(location + ".mogg", out var moggListing))
            {
                return null;
            }

            var entry = new PackedRBCONEntry(conInfo, nodeName)
            {
                _subName = subname,
                _midiListing = midiListing,
                _moggListing = moggListing,
                _psuedoDirectory = Path.Combine(conInfo.FullName, listings[midiListing.PathIndex].Name)
            };
            entry.Deserialize(ref stream, strings);

            string genPath = $"songs/{entry._subName}/gen/{entry._subName}";
            listings.FindListing(genPath + ".milo_xbox", out entry._miloListing);
            listings.FindListing(genPath + "_keep.png_xbox", out entry._imgListing);
            return entry;
        }

        public static PackedRBCONEntry ForceDeserialize(List<CONFileListing>? listings, in AbridgedFileInfo conInfo, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            var entry = new PackedRBCONEntry(conInfo, nodeName)
            {
                _subName = stream.ReadString(),
            };
            entry.Deserialize(ref stream, strings);

            entry._psuedoDirectory = Path.Combine(conInfo.FullName, $"songs/{entry._subName}");
            if (listings != null)
            {
                string location = $"songs/{entry._subName}/{entry._subName}";
                listings.FindListing(location + ".mid", out entry._midiListing);
                listings.FindListing(location + ".mogg", out entry._moggListing);


                string genPath = $"songs/{entry._subName}/gen/{entry._subName}";
                listings.FindListing(genPath + ".milo_xbox", out entry._miloListing);
                listings.FindListing(genPath + "_keep.png_xbox", out entry._imgListing);
            }
            return entry;
        }
    }
}
