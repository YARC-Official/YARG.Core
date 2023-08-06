using System;
using System.Text;

namespace YARG.Core.Song
{
    public unsafe class Midi_FiveFret : MidiInstrument_Common
    {
        internal static readonly byte[][] ENHANCED_STRINGS = new byte[][] { Encoding.ASCII.GetBytes("[ENHANCED_OPENS]"), Encoding.ASCII.GetBytes("ENHANCED_OPENS") };

        private readonly bool[,] notes = new bool[4, 6];
        private readonly int[] lanes = {
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        protected override bool IsNote() { return 59 <= note.value && note.value <= 107; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = lanes[noteValue];
                if (lane < 6)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = lanes[noteValue];
                if (lane < 6 && notes[diffIndex, lane])
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        protected override void ParseSysEx(ReadOnlySpan<byte> str)
        {
            if (str.StartsWith(SYSEXTAG) && str[5] == 1)
            {
                int status = str[6] == 0 ? 1 : 0;
                if (str[4] == (char)0xFF)
                {
                    for (int diff = 0; diff < 4; ++diff)
                        lanes[12 * diff + 1] = status;
                }
                else
                    lanes[12 * str[4] + 1] = status;
            }
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.SequenceEqual(ENHANCED_STRINGS[0]) || str.SequenceEqual(ENHANCED_STRINGS[1]))
                for (int diff = 0; diff < 4; ++diff)
                    lanes[12 * diff] = 0;
        }
    }
}
