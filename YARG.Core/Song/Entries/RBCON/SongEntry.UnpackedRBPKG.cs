using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    internal sealed class UnpackedRBPKGEntry : UnpackedConsolePackageEntry
    {
        private const int    ENCRYPTED_EDAT_MAGIC       = 0x4E504400;
        protected override YARGImage DXTImageLoader(string path) => YARGImage.LoadPS3DXT(path);

        private UnpackedRBPKGEntry(in AbridgedFileInfo root, string nodeName)
            : base(in root, nodeName) {}

        public static ScanExpected<RBCONEntry> Create(in RBScanParameters parameters)
        {
            try
            {
                var entry = new UnpackedRBPKGEntry(in parameters.Root, parameters.NodeName)
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

                entry._subName = location.Value[6..location.Value.IndexOf('/', 6)];

                string songDirectory = Path.Combine(parameters.Root.FullName, entry._subName);

                var midiInfo = new FileInfo(Path.Combine(songDirectory, entry._subName + ".mid.edat"));
                if (!midiInfo.Exists)
                {
                    return new ScanUnexpected(ScanResult.MissingCONMidi);
                }
                // First three bytes should be 4E 50 44 if encrypted
                using var midiStream = new FileStream(midiInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                if ((midiStream.Read<int>(Endianness.Big) & 0xFFFFFF00) == ENCRYPTED_EDAT_MAGIC)
                {
                    return new ScanUnexpected(ScanResult.EdatMidiEncrypted);
                }

                string moggPath = Path.Combine(songDirectory, entry._subName + ".mogg");
                if (File.Exists(moggPath))
                {
                    using var moggStream = new FileStream(moggPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                    var moggResult = ValidateMoggHeader(moggStream);
                    if (moggResult != ScanResult.Success)
                    {
                        return new ScanUnexpected(moggResult);
                    }
                }
                else
                {
                    return new ScanUnexpected(ScanResult.MoggError);
                }

                using var mainMidi = FixedArray.LoadFile(midiInfo.FullName);

                var result = ScanMidis(entry, mainMidi);
                if (result != ScanResult.Success)
                {
                    return new ScanUnexpected(result);
                }
                entry._midiLastWrite = AbridgedFileInfo.NormalizedLastWrite(midiInfo);
                entry.SetSortStrings();
                return entry;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
                return new ScanUnexpected(ScanResult.DTAError);
            }
        }

        public static UnpackedRBPKGEntry? TryDeserialize(in AbridgedFileInfo root, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string subname = stream.ReadString();
            string midiPath = Path.Combine(root.FullName, subname, subname + ".mid.edat");
            var midiInfo = new FileInfo(midiPath);
            if (!midiInfo.Exists)
            {
                return null;
            }

            var midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            if (midiLastWrite != midiInfo.LastWriteTime)
            {
                return null;
            }

            var entry = new UnpackedRBPKGEntry(in root, nodeName)
            {
                _subName = subname,
                _midiLastWrite = midiLastWrite,
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static UnpackedRBPKGEntry ForceDeserialize(in AbridgedFileInfo root, string nodeName, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            var entry = new UnpackedRBPKGEntry(in root, nodeName)
            {
                _subName = stream.ReadString(),
                _midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little)),
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public override YARGImage? LoadAlbumData() => LoadAlbumData(".png_ps3");

        public override FixedArray<byte>? LoadMiloData() => LoadMiloData(".milo_ps3");

        protected override FixedArray<byte>? GetMainMidiData() => GetMainMidiData(".mid.edat");
    }
}
