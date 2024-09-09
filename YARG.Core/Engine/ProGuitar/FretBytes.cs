namespace YARG.Core.Engine.ProGuitar
{
    public struct FretBytes
    {
        public const byte IGNORE_BYTE = 0xFF;

        private ulong _bytes;

        private FretBytes(ulong bytes)
        {
            _bytes = bytes;
        }

        // Some good ol' bit manip
        public byte this[int index]
        {
            get => (byte) (_bytes >> (index * 8));
            set => _bytes = (_bytes & ~(0xFFUL << (index * 8))) | ((ulong) value << (index * 8));
        }

        public void Clear()
        {
            _bytes = 0;
        }

        public static FretBytes CreateEmpty()
        {
            return new FretBytes(0);
        }

        public static FretBytes CreateMask()
        {
            // All "ignore bytes"
            return new FretBytes(ulong.MaxValue);
        }

        public static bool IsFretted(FretBytes hand, FretBytes chord)
        {
            for (int i = 0; i < sizeof(ulong); i++)
            {
                byte chordByte = chord[i];
                if (chordByte == IGNORE_BYTE)
                {
                    continue;
                }

                if (hand[i] != chordByte)
                {
                    return false;
                }
            }

            return true;
        }
    }
}