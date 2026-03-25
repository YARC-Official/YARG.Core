using System;
using System.IO;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal abstract class UnpackedConsolePackageEntryGroup : CONEntryGroup
    {
        private UnpackedConsolePackageEntryGroup(in AbridgedFileInfo root, string defaultPlaylist)
            : base(root, defaultPlaylist) { }

        public static bool Create(string directory, FileInfo dtaInfo, string defaultPlaylist, out CONEntryGroup group)
        {
            try
            {
                // Peek at the DTA just to detect platform, don't hold onto the data
                using var data = FixedArray.LoadFile(dtaInfo.FullName);
                var container = YARGDTAReader.Create(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);

                    if (File.Exists(Path.Combine(directory, name, name + ".mid.edat")))
                    {
                        return UnpackedPKGEntryGroup.Create(directory, dtaInfo, defaultPlaylist, out group);
                    }
                    if (File.Exists(Path.Combine(directory, name, name + ".mid")))
                    {
                        return UnpackedCONEntryGroup.Create(directory, dtaInfo, defaultPlaylist, out group);
                    }
                    YargLogger.LogWarning("Node contained neither .mid nor .mid.edat, cannot determine entry group, checking next node if available");
                    YARGDTAReader.EndNode(ref container);
                }

                group = null!;
                return false;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e);
            }
            group = null!;
            return false;
        }
    }
}
