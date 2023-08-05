﻿namespace YARG.Core.Song
{
    public struct PartValues
    {
        public byte subTracks;
        public sbyte intensity;
        public PartValues(sbyte baseIntensity)
        {
            subTracks = 0;
            intensity = baseIntensity;
        }

        public void Set(int subTrack)
        {
            subTracks |= (byte) (1 << subTrack);
        }

        public bool this[int subTrack]
        {
            get { return ((byte) (1 << subTrack) & subTracks) > 0; }
        }

        public static PartValues operator |(PartValues lhs, PartValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
