using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public class Midi_UnknownDrums : Midi_Drum
    {
        private const int NUM_LANES = MAX_NUMPADS;
        private const int FIVE_LANE_DRUM = 6;
        private DrumPreparseType _type = DrumPreparseType.FourLane;
        public DrumPreparseType Type { get { return _type; } }

        public Midi_UnknownDrums(DrumPreparseType type)
        {
            _type = type;
        }

        protected override bool IsFullyScanned() { return validations == FULL_VALIDATION && _type != DrumPreparseType.FourLane; }
        protected override bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= FIVELANE_MAX; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - DEFAULT_MIN;
            int laneIndex = LANEINDICES[noteValue];
            if (laneIndex < NUM_LANES)
            {
                statuses[DIFFVALUES[noteValue], laneIndex] = true;
                if (laneIndex == FIVE_LANE_DRUM)
                {
                    _type = DrumPreparseType.FiveLane;
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
                _type = DrumPreparseType.FourPro;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
