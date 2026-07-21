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
                using var data = FixedArray.LoadFile(dtaInfo.FullName);
                var container = YARGDTAReader.Create(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    // This is an expensive operation, but happens only once per CON entry group, and is required to parse location
                    var entry = DTAEntry.Create(name, container);
                    if (entry.Location is null)
                    {
                        YargLogger.LogFormatWarning("Node {0} could not be parsed as a DTA entry, cannot determine entry group, checking next node if available", name);
                        YARGDTAReader.EndNode(ref container);
                        continue;
                    }
                    string subname = entry.Location[6..entry.Location.IndexOf('/', 6)];
                    if (File.Exists(Path.Combine(directory, subname, subname + ".mid.edat")))
                    {
                        return UnpackedPKGEntryGroup.Create(directory, dtaInfo, defaultPlaylist, out group);
                    }
                    if (File.Exists(Path.Combine(directory, subname, subname + ".mid")))
                    {
                        return UnpackedCONEntryGroup.Create(directory, dtaInfo, defaultPlaylist, out group);
                    }
                    YargLogger.LogFormatWarning("Node {0} contained neither .mid nor .mid.edat, cannot determine entry group, checking next node if available", name);
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
