namespace YARG.Core.Song
{
    public abstract class Midi_ProGuitar : MidiInstrumentPreparser
    {
        private const int NOTES_PER_DIFFICULTY = 24;
        private const int PROGUITAR_MAX = PROGUITAR_MIN + NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY;
        protected const int PROGUITAR_MIN = 24;
        protected const int NUM_STRINGS = 6;
        protected const int MIN_VELOCITY = 100;
        protected const int ARPEGGIO_CHANNEL = 1;
        protected static readonly int[] DIFFVALUES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        protected static readonly int[] LANEINDICES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        };

        protected readonly bool[] diffultyTracker = new bool[NUM_DIFFICULTIES];
        protected readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_STRINGS];

        protected override bool IsNote() { return PROGUITAR_MIN <= note.value && note.value <= PROGUITAR_MAX; }

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

    public class Midi_ProGuitar17 : Midi_ProGuitar
    {
        private const int MAX_VELOCITY = 117;
        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - PROGUITAR_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!diffultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_STRINGS && currEvent.channel != ARPEGGIO_CHANNEL && MIN_VELOCITY <= note.velocity && note.velocity <= MAX_VELOCITY)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }
    }

    public class Midi_ProGuitar22 : Midi_ProGuitar
    {
        private const int MAX_VELOCITY = 122;
        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - PROGUITAR_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!diffultyTracker[diffIndex])
            {
                int laneIndex = LANEINDICES[noteValue];
                if (laneIndex < NUM_STRINGS && currEvent.channel != ARPEGGIO_CHANNEL && MIN_VELOCITY <= note.velocity && note.velocity <= MAX_VELOCITY)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }
    }
}
