using System;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal abstract class RBProUpgrade
    {
        public const string UPGRADES_DTA = "upgrades.dta";
        public const string UPGRADES_MIDI_EXT = "_plus.mid";

        public abstract DateTime LastWriteTime { get; }
        public abstract FixedArray<byte> LoadUpgradeMidi();

        protected readonly AbridgedFileInfo _root;
        protected RBProUpgrade(in AbridgedFileInfo root)
        {
            _root = root;
        }
    }

    [Serializable]
    internal sealed class PackedRBProUpgrade : RBProUpgrade
    {
        public const string UPGRADES_DIRECTORY = "songs_upgrades/";

        private readonly CONFileListing? _listing;

        public override DateTime LastWriteTime => _root.LastWriteTime;

        public PackedRBProUpgrade(CONFileListing? listing, in AbridgedFileInfo root)
            : base(in root)
        {
            _listing = listing;
        }

        public override FixedArray<byte> LoadUpgradeMidi()
        {
            return _listing != null && _root.IsStillValid()
                ? CONFileStream.LoadFile(_root.FullName, _listing)
                : FixedArray<byte>.Null;
        }
    }

    [Serializable]
    internal sealed class UnpackedRBProUpgrade : RBProUpgrade
    {
        private readonly string _name;
        private readonly DateTime _lastWritetime;

        public override DateTime LastWriteTime => _lastWritetime;

        public UnpackedRBProUpgrade(string name, in DateTime lastWriteTime, in AbridgedFileInfo root)
            : base(root)
        {
            _name = name;
            _lastWritetime = lastWriteTime;
        }

        public override FixedArray<byte> LoadUpgradeMidi()
        {
            var data = FixedArray<byte>.Null;
            if (AbridgedFileInfo.Validate(Path.Combine(_root.FullName, UPGRADES_DTA), in _root.LastWriteTime))
            {
                string file = Path.Combine(_root.FullName, _name + UPGRADES_MIDI_EXT);
                if (AbridgedFileInfo.Validate(file, in _lastWritetime))
                {
                    data = FixedArray.LoadFile(file);
                }
            }
            return data;
        }
    }
}
