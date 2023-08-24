using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public class Midi_UnknownDrums : Midi_Drum
    {
        private const int NUM_LANES = MAX_NUMPADS;
        private const int FIVE_LANE_DRUM = 6;
        private DrumsType type = DrumsType.FourLane;

        private Midi_UnknownDrums(DrumsType type)
        {
            this.type = type;
        }

        public static (DifficultyMask, DrumsType) Parse(YARGMidiReader reader, DrumsType type)
        {
            Midi_UnknownDrums preparser = new(type);
            preparser.Process(reader);
            return ((DifficultyMask) preparser.validations, preparser.type);
        }

        protected override bool IsFullyScanned() { return validations == FULL_VALIDATION && type != DrumsType.FourLane; }
        protected override bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= FIVELANE_MAX; }

        protected override bool ParseLaneColor_ON()
        {
            int noteValue = note.value - DEFAULT_MIN;
            int laneIndex = LANEINDICES[noteValue];
            if (laneIndex < NUM_LANES)
            {
                statuses[DIFFVALUES[noteValue], laneIndex] = true;
                if (laneIndex == FIVE_LANE_DRUM)
                {
                    type = DrumsType.FiveLane;
                    return IsFullyScanned();
                }
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

        protected override bool ToggleExtraValues()
        {
            if (YELLOW_FLAG <= note.value && note.value <= GREEN_FLAG)
            {
                type = DrumsType.ProDrums;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
