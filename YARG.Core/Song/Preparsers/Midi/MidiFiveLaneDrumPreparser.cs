using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public class Midi_FiveLane_Preparser : Midi_Drum_Preparser_Base
    {
        private const int NUM_LANES = MAX_NUMPADS;
        protected override bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= FIVELANE_MAX; }

        protected override bool IsFullyScanned() { return validations == ALL_DIFFICULTIES_PLUS; }

        private Midi_FiveLane_Preparser() { }

        public static DifficultyMask Parse(YARGMidiReader reader)
        {
            Midi_FiveLane_Preparser preparser = new();
            preparser.Process(reader);
            return preparser.validations;
        }

        protected override bool ParseLaneColor_ON()
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
