using System;

namespace YARG.Core.IO
{
    public sealed class CONFileListing
    {
        [Flags]
        public enum Flag
        {
            Consecutive = 0x40,
            Directory = 0x80,
        }

        public string Name = string.Empty;
        public Flag Flags;
        public int BlockCount;
        public int BlockOffset;
        public short PathIndex;
        public int Length;
        public DateTime LastWrite;
        public int Shift;

        public bool IsContiguous() { return (Flags & Flag.Consecutive) > 0; }
        public bool IsDirectory() { return (Flags & Flag.Directory) > 0; }
    }
}