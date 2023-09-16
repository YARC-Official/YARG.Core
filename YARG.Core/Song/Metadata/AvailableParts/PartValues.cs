using System;

namespace YARG.Core.Song
{
    [Serializable]
    public struct PartValues
    {
        public byte subTracks;
        public sbyte intensity;
        public PartValues(sbyte baseIntensity)
        {
            subTracks = 0;
            intensity = baseIntensity;
        }

        public bool this[int subTrack]
        {
            get
            {
                if (subTrack >= 5)
                    throw new System.Exception("Subtrack index out of range");
                return ((byte) (1 << subTrack) & subTracks) > 0;
            }
        }

        public bool this[Difficulty difficulty] => this[(int) difficulty];

        public DifficultyMask Difficulties
        {
            get => (DifficultyMask) subTracks;
            set => subTracks = (byte) value;
        }

        public void SetSubtrack(int subTrack)
        {
            subTracks |= (byte) (1 << subTrack);
        }

        public void SetDifficulty(Difficulty difficulty)
            => SetSubtrack((int) difficulty);

        public bool WasParsed() { return subTracks > 0; }

        public static PartValues operator |(PartValues lhs, PartValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
