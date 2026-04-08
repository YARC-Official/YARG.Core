using System;
using System.Runtime.InteropServices;

namespace YARG.Core.Song
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct PartValues
    {
        public static readonly PartValues Default = new()
        {
            SubTracks = 0,
            Difficulties = DifficultyMask.None,
            Intensity = -1
        };

        [FieldOffset(0)] public byte SubTracks;
        [FieldOffset(0)] public DifficultyMask Difficulties;

        [FieldOffset(1)] public sbyte Intensity;

        public readonly bool this[int subTrack]
        {
            get
            {
                const int BITS_IN_BYTE = 8;
                if (subTrack >= BITS_IN_BYTE)
                {
                    throw new IndexOutOfRangeException("Subtrack out of range");
                }
                return ((1 << subTrack) & SubTracks) > 0;
            }
        }

        public readonly bool this[Difficulty difficulty]
        {
            get
            {
                if (difficulty < Difficulty.Beginner || difficulty > Difficulty.ExpertPlus)
                {
                    throw new Exception("Difficulty out of range");
                }
                return ((1 << (int)difficulty) & SubTracks) > 0;
            }
        }

        public void ActivateSubtrack(int subTrack)
        {
            SubTracks |= (byte) (1 << subTrack);
        }

        public void ActivateDifficulty(Difficulty difficulty)
        {
            Difficulties |= (DifficultyMask) (1 << (int)difficulty);
        }

        public readonly bool IsActive() { return SubTracks > 0; }
    }
}
