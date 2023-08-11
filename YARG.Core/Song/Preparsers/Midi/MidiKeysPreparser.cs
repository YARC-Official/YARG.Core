namespace YARG.Core.Song
{
    public class Midi_Keys : MidiInstrument_Common
    {
        private const int NUM_LANES = 5;
        private static readonly int[] LANEINDICES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY] {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        private readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_LANES];

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - DEFAULT_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_LANES)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - DEFAULT_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_LANES)
                {
                    Validate(diffIndex);
                    difficultyTracker[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }
}
