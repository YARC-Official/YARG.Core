using System;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    internal class CONModification
    {
        public bool Processed = false;
        public DTAEntry UpdateDTA = DTAEntry.Empty;
        public DTAEntry UpgradeDTA = DTAEntry.Empty;
        public AbridgedFileInfo? UpdateDirectoryAndDtaLastWrite;
        public DateTime? UpdateMidiLastWrite;
        public RBProUpgrade? Upgrade;
    }

    internal class QuickCONMods
    {
        public AbridgedFileInfo? UpdateDirectoryAndDtaLastWrite;
        public DateTime? UpdateMidi;
        public RBProUpgrade? Upgrade;
    }
}
