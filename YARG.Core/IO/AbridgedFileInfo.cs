using System;
using System.IO;

namespace YARG.Core.IO
{
    public sealed class AbridgedFileInfo
    {
        public readonly string FullName;
        public readonly DateTime LastWriteTime;

        public AbridgedFileInfo(string file) : this(file, File.GetLastWriteTime(file)) { }

        public AbridgedFileInfo(FileInfo info) : this(info.FullName, info.LastWriteTime) { }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(YARGBinaryReader reader)
        {
            FullName = reader.ReadLEBString();
            LastWriteTime = DateTime.FromBinary(reader.ReadInt64());
        }

        public AbridgedFileInfo(string fullname, DateTime lastWrite)
        {
            FullName = fullname;
            LastWriteTime = lastWrite;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(FullName);
            writer.Write(LastWriteTime.ToBinary());
        }

        public bool IsStillValid()
        {
            FileInfo file = new(FullName);
            return file.Exists && file.LastWriteTime == LastWriteTime;
        }

        public static implicit operator AbridgedFileInfo(FileInfo info) => new(info);

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(YARGBinaryReader reader)
        {
            return TryParseInfo(reader.ReadLEBString(), reader);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(string file, YARGBinaryReader reader)
        {
            FileInfo midiInfo = new(file);
            if (!midiInfo.Exists || midiInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return midiInfo;
        }
    }
}
