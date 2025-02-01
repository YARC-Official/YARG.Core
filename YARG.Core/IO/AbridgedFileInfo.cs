using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    /// <summary>
    /// A FileInfo structure that only contains the filename and time last added
    /// </summary>
    public readonly struct AbridgedFileInfo
    {
        /// <summary>
        /// The file path
        /// </summary>
        public readonly string FullName;

        /// <summary>
        /// The time the file was last written or created on OS - whichever came later
        /// </summary>
        public readonly DateTime LastWriteTime;

        public AbridgedFileInfo(string file)
            : this(new FileInfo(file)) {}

        public AbridgedFileInfo(FileInfo info)
        {
            FullName = info.FullName;
            LastWriteTime = NormalizedLastWrite(info);
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(ref FixedArrayStream stream)
        {
            FullName = stream.ReadString();
            LastWriteTime = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(string filename, ref FixedArrayStream stream)
        {
            FullName = filename;
            LastWriteTime = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
        }

        public AbridgedFileInfo(string filename, in DateTime lastUpdatedTime)
        {
            FullName = filename;
            LastWriteTime = lastUpdatedTime;
        }

        public void Serialize(MemoryStream stream)
        {
            stream.Write(FullName);
            stream.Write(LastWriteTime.ToBinary(), Endianness.Little);
        }

        public bool Exists()
        {
            return File.Exists(FullName);
        }

        public bool IsStillValid()
        {
            return Validate(FullName, in LastWriteTime);
        }

        public static DateTime NormalizedLastWrite(FileInfo info)
        {
            return info.LastWriteTime > info.CreationTime ? info.LastWriteTime : info.CreationTime;
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static bool TryParseInfo(ref FixedArrayStream stream, out AbridgedFileInfo abridged)
        {
            return TryParseInfo(stream.ReadString(), ref stream, out abridged);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static bool TryParseInfo(string file, ref FixedArrayStream stream, out AbridgedFileInfo abridged)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                stream.Position += sizeof(long);
                abridged = default;
                return false;
            }

            abridged = new AbridgedFileInfo(info);
            return abridged.LastWriteTime == DateTime.FromBinary(stream.Read<long>(Endianness.Little));
        }

        public static bool Validate(string file, in DateTime lastWrite)
        {
            var info = new FileInfo(file);
            return info.Exists && NormalizedLastWrite(info) == lastWrite;
        }
    }
}
