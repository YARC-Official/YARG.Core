using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_ProGuitar_Preparser : Midi_Instrument_Preparser
    {
        private const int NOTES_PER_DIFFICULTY = 24;
        private const int PROGUITAR_MAX = PROGUITAR_MIN + NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY;
        private const int PROGUITAR_MIN = 24;
        private const int NUM_STRINGS = 6;
        private const int MIN_VELOCITY = 100;
        private const int ARPEGGIO_CHANNEL = 1;
        private static readonly int[] DIFFVALUES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        private static readonly int[] LANEINDICES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        };

        private readonly bool[] diffultyTracker = new bool[NUM_DIFFICULTIES];
        private readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_STRINGS];
        private readonly int maxVelocity;

        private Midi_ProGuitar_Preparser(int maxVelocity)
        {
            this.maxVelocity = maxVelocity;
        }

        public static DifficultyMask Parse_17Fret(YARGMidiReader reader)
        {
            Midi_ProGuitar_Preparser preparser = new(117);
            preparser.Process(reader);
            return preparser.validations;
        }

        public static DifficultyMask Parse_22Fret(YARGMidiReader reader)
        {
            Midi_ProGuitar_Preparser preparser = new(122);
            preparser.Process(reader);
            return preparser.validations;
        }

        protected override bool IsNote() { return PROGUITAR_MIN <= note.value && note.value <= PROGUITAR_MAX; }

        protected override bool ParseLaneColor_ON()
        {
            int noteValue = note.value - PROGUITAR_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!diffultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_STRINGS && currEvent.channel != ARPEGGIO_CHANNEL && MIN_VELOCITY <= note.velocity && note.velocity <= maxVelocity)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - PROGUITAR_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!diffultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_STRINGS && currEvent.channel != ARPEGGIO_CHANNEL)
                {
                    Validate(diffIndex);
                    diffultyTracker[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }
}
