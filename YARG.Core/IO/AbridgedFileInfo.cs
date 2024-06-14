﻿using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public interface IAbridgedInfo
    {
        public string FullName { get; }
        public DateTime LastUpdatedTime { get; }
    }

    /// <summary>
    /// A FileInfo structure that only contains the filename and time last added
    /// </summary>
    public readonly struct AbridgedFileInfo : IAbridgedInfo
    {
        public const FileAttributes RECALL_ON_DATA_ACCESS = (FileAttributes)0x00400000;

        /// <summary>
        /// The file path
        /// </summary>
        private readonly string _fullname;

        /// <summary>
        /// The time the file was last written or created on OS - whichever came later
        /// </summary>
        private readonly DateTime _lastUpdatedTime;

        public string FullName => _fullname;
        public DateTime LastUpdatedTime => _lastUpdatedTime;

        public AbridgedFileInfo(string file, bool checkCreationTime = true)
            : this(new FileInfo(file), checkCreationTime) { }

        public AbridgedFileInfo(FileInfo info, bool checkCreationTime = true)
        {
            _fullname = info.FullName;
            _lastUpdatedTime = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > _lastUpdatedTime)
            {
                _lastUpdatedTime = info.CreationTime;
            }
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(BinaryReader reader)
            : this(reader.ReadString(), reader) { }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo(string filename, BinaryReader reader)
        {
            _fullname = filename;
            _lastUpdatedTime = DateTime.FromBinary(reader.ReadInt64());
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(_fullname);
            writer.Write(_lastUpdatedTime.ToBinary());
        }

        public bool Exists()
        {
            return File.Exists(_fullname);
        }

        public bool IsStillValid(bool checkCreationTime = true)
        {
            var info = new FileInfo(_fullname);
            if (!info.Exists)
            {
                return false;
            }

            var timeToCompare = info.LastWriteTime;
            if (checkCreationTime && info.CreationTime > timeToCompare)
            {
                timeToCompare = info.CreationTime;
            }
            return timeToCompare == _lastUpdatedTime;
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(BinaryReader reader, bool checkCreationTime = true)
        {
            return TryParseInfo(reader.ReadString(), reader, checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo? TryParseInfo(string file, BinaryReader reader, bool checkCreationTime = true)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                reader.BaseStream.Position += sizeof(long);
                return null;
            }

            var abridged = new AbridgedFileInfo(info, checkCreationTime);
            if (abridged._lastUpdatedTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                return null;
            }
            return abridged;
        }
    }

    public readonly struct AbridgedFileInfo_Length : IAbridgedInfo
    {
        private readonly AbridgedFileInfo _base;

        /// <summary>
        /// The length of the file in bytes
        /// </summary>
        public readonly long Length;

        public string FullName => _base.FullName;
        public DateTime LastUpdatedTime => _base.LastUpdatedTime;

        public AbridgedFileInfo_Length(string file, bool checkCreationTime = true)
            : this(new FileInfo(file), checkCreationTime) { }

        public AbridgedFileInfo_Length(FileInfo info, bool checkCreationTime = true)
        {
            _base = new AbridgedFileInfo(info, checkCreationTime);
            Length = info.Length;
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo_Length(BinaryReader reader)
        {
            _base = new AbridgedFileInfo(reader);
            Length = reader.ReadInt64();
        }

        /// <summary>
        /// Only used when validation of the underlying file is not required
        /// </summary>
        public AbridgedFileInfo_Length(string filename, BinaryReader reader)
        {
            _base = new AbridgedFileInfo(filename, reader);
            Length = reader.ReadInt64();
        }

        private AbridgedFileInfo_Length(in AbridgedFileInfo info, long length)
        {
            _base = info;
            Length = length;
        }

        public void Serialize(BinaryWriter writer)
        {
            _base.Serialize(writer);
            writer.Write(Length);
        }

        public bool Exists()
        {
            return File.Exists(_base.FullName);
        }

        public bool IsStillValid(bool checkCreationTime = true)
        {
            return _base.IsStillValid(checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo_Length? TryParseInfo(BinaryReader reader, bool checkCreationTime = true)
        {
            return TryParseInfo(reader.ReadString(), reader, checkCreationTime);
        }

        /// <summary>
        /// Used for cache validation
        /// </summary>
        public static AbridgedFileInfo_Length? TryParseInfo(string file, BinaryReader reader, bool checkCreationTime = true)
        {
            var info = AbridgedFileInfo.TryParseInfo(file, reader, checkCreationTime);
            if (info == null)
            {
                return null;
            }
            return new AbridgedFileInfo_Length(info.Value, reader.ReadInt64());
        }
    }
}
