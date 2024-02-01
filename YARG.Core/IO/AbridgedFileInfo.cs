using System;
using System.IO;

namespace YARG.Core.IO
{
    /// <summary>
    /// A FileInfo structure that only contains the filename and time last added
    /// </summary>
    public sealed class AbridgedFileInfo
    {
        public const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x00400000;

        /// <summary>
        /// The flie path
        /// </summary>
        public readonly string FullName;

        /// <summary>
        /// The time the file was last written or created on OS - whichever came later
        /// </summary>
        public readonly DateTime LastUpdatedTime;

        public AbridgedFileInfo(string file, bool checkCreationTime = true)
            : this(new FileInfo(file), checkCreationTime) { }

        public AbridgedFileInfo(FileInfo info, bool checkCreationTime = true)
        {
            FullName = info.FullName;
            LastUpdatedTime = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > LastUpdatedTime)
            {
                LastUpdatedTime = info.CreationTime;
            }
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(YARGBinaryReader reader)
            : this(reader.ReadLEBString(), reader) { }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(string filename, YARGBinaryReader reader)
        {
            FullName = filename;
            LastUpdatedTime = DateTime.FromBinary(reader.Read<long>(Endianness.Little));
        }

        public AbridgedFileInfo(string fullname, DateTime timeAdded)
        {
            FullName = fullname;
            LastUpdatedTime = timeAdded;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(FullName);
            writer.Write(LastUpdatedTime.ToBinary());
        }

        public bool Exists()
        {
            return File.Exists(FullName);
        }

        public bool IsStillValid(bool checkCreationTime = true)
        {
            var info = new FileInfo(FullName);
            if (!info.Exists)
            {
                return false;
            }

            var timeToCompare = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > timeToCompare)
            {
                timeToCompare = info.CreationTime;
            }
            return timeToCompare == LastUpdatedTime;
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(YARGBinaryReader reader, bool checkCreationTime = true)
        {
            return TryParseInfo(reader.ReadLEBString(), reader, checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(string file, YARGBinaryReader reader, bool checkCreationTime = true)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                return null;
            }

            var abridged = new AbridgedFileInfo(info, checkCreationTime);
            if (abridged.LastUpdatedTime != DateTime.FromBinary(reader.Read<long>(Endianness.Little)))
            {
                return null;
            }
            return abridged;
        }
    }
}
