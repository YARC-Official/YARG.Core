namespace YARG.Core.Song
{
    public abstract class Midi_ProGuitar : MidiInstrument
    {
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        internal static readonly int[] LANEVALUES = {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        };

        protected readonly bool[] difficulties = new bool[4];
        protected readonly bool[,] notes = new bool[4, 6];

        protected override bool IsNote() { return 24 <= note.value && note.value <= 106; }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1)
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }

    public class Midi_ProGuitar17 : Midi_ProGuitar
    {
        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1 && 100 <= note.velocity && note.velocity <= 117)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }
    }

    public class Midi_ProGuitar22 : Midi_ProGuitar
    {
        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1 && 100 <= note.velocity && note.velocity <= 122)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }
    }
}
