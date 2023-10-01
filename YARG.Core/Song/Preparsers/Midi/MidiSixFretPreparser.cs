using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_SixFret_Preparser : MidiInstrument_Common
    {
        // Open note included
        private const int NUM_LANES = 7;
        private static readonly int[] LANEINDICES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY] {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        private readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_LANES];

        private Midi_SixFret_Preparser() { }

        public static DifficultyMask Parse(YARGMidiReader reader)
        {
            Midi_SixFret_Preparser preparser = new();
            preparser.Process(reader);
            return preparser.validations;
        }

        protected override bool IsNote() { return 58 <= note.value && note.value <= 103; }

        protected override bool ParseLaneColor_ON()
        {
            int noteValue = note.value - 58;
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
            int noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_LANES && statuses[diffIndex, laneIndex])
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
