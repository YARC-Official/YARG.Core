namespace YARG.Core.Song
{
    public class Midi_SixFret : MidiInstrument_Common
    {
        private readonly bool[,] notes = new bool[4, 7];
        private static readonly uint[] LANEVALUES = new uint[] {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        protected override bool IsNote() { return 58 <= note.value && note.value <= 103; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 58;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 7)
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
                uint lane = LANEVALUES[noteValue];
                if (lane < 7 && notes[diffIndex, lane])
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }
}
