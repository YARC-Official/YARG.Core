namespace YARG.Core.Song
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
            get
            {
                if (subTracks >= 5)
                    throw new System.Exception("Subtrack index out of range");
                return ((byte) (1 << subTrack) & subTracks) > 0;
            }
        }

        public bool IsParsed() { return subTracks > 0; }

        public static PartValues operator |(PartValues lhs, PartValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
