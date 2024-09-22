﻿namespace YARG.Core.IO
{
    public readonly struct SngFileListing
    {
        public readonly string Name;
        public readonly long Position;
        public readonly long Length;

        public SngFileListing(string name, long position, long length)
        {
            Name = name;
            Position = position;
            Length = length;
        }

        public FixedArray<byte> LoadAllBytes(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return SngFileStream.LoadFile(stream, sngFile.Mask, Length, Position);
        }

        public SngFileStream CreateStream(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return new SngFileStream(Name, stream, sngFile.Mask, Length, Position);
        }
    }
}
