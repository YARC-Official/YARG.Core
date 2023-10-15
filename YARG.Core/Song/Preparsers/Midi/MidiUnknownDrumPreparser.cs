using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_UnknownDrums_Preparser : Midi_Drum_Preparser_Base
    {
        private const int NUM_LANES = MAX_NUMPADS;
        private const int FIVE_LANE_DRUM = 6;
        private DrumsType type = DrumsType.FourLane;

        private Midi_UnknownDrums_Preparser(DrumsType type)
        {
            this.type = type;
        }

        public static (DifficultyMask, DrumsType) Parse(YARGMidiTrack track, DrumsType type)
        {
            Midi_UnknownDrums_Preparser preparser = new(type);
            preparser.Process(track);
            return (preparser.validations, preparser.type);
        }

        protected override bool IsFullyScanned() { return validations == ALL_DIFFICULTIES_PLUS && type != DrumsType.FourLane; }
        protected override bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= FIVELANE_MAX; }

        protected override bool ParseLaneColor_ON(YARGMidiTrack track)
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

        protected override bool ParseLaneColor_Off(YARGMidiTrack track)
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

        protected override bool ToggleExtraValues(YARGMidiTrack track)
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
