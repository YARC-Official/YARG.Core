using System;
using System.IO;
using YARG.Core.Extensions;

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

        /// <summary>
        /// The length of the file in bytes
        /// </summary>
        public readonly long Length;

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
            Length = info.Length;
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(BinaryReader reader, bool readLength)
            : this(reader.ReadString(), reader, readLength) { }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(string filename, BinaryReader reader, bool readLength)
        {
            FullName = filename;
            LastUpdatedTime = DateTime.FromBinary(reader.ReadInt64());
            Length = readLength ? reader.ReadInt64() : 0;
        }

        public AbridgedFileInfo(string fullname, DateTime timeAdded, long length)
        {
            FullName = fullname;
            LastUpdatedTime = timeAdded;
            Length = length;
        }

        public void Serialize(BinaryWriter writer, bool writeLength)
        {
            writer.Write(FullName);
            writer.Write(LastUpdatedTime.ToBinary());
            if (writeLength)
            {
                writer.Write(Length);
            }
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
        public static AbridgedFileInfo? TryParseInfo(BinaryReader reader, bool hasLength, bool checkCreationTime = true)
        {
            return TryParseInfo(reader.ReadString(), reader, hasLength, checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(string file, BinaryReader reader, bool hasLength, bool checkCreationTime = true)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                reader.BaseStream.Position += sizeof(long);
                return null;
            }

            var abridged = new AbridgedFileInfo(info, checkCreationTime);
            if (abridged.LastUpdatedTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                return null;
            }

            if (hasLength)
            {
                reader.BaseStream.Position += sizeof(long);
            }
            return abridged;
        }
    }
}
