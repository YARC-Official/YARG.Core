using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_SixFret_Preparser : MidiInstrument_Common
    {
        // Open note included
        private const int NOTE_MIN = 58;
        private const int NOTE_MAX = 103;
        private const int NUM_LANES = 7;
        private static readonly int[] LANEINDICES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY] {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        private readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_LANES];

        private Midi_SixFret_Preparser() { }

        public static DifficultyMask Parse(YARGMidiTrack track)
        {
            Midi_SixFret_Preparser preparser = new();
            preparser.Process(track);
            return preparser.validations;
        }

        protected override bool IsNote() { return NOTE_MIN <= note.value && note.value <= NOTE_MAX; }

        protected override bool ParseLaneColor_ON(YARGMidiTrack track)
        {
            int noteValue = note.value - NOTE_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_LANES)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off(YARGMidiTrack track)
        {
            int noteValue = note.value - NOTE_MIN;
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
