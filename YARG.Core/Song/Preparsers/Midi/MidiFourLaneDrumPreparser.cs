using YARG.Core.Chart;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public class Midi_FourLaneDrum : Midi_Drum
    {
        private const int NUM_LANES = MAX_NUMPADS - 1;
        private DrumsType _type;
        public DrumsType Type => _type;
        protected Midi_FourLaneDrum(DrumsType type)
        {
            _type = type;
        }

        public Midi_FourLaneDrum() : this(DrumsType.FourLane) { }

        protected override bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= 100; }

        protected override bool IsFullyScanned() { return validations == FULL_VALIDATION && _type == DrumsType.ProDrums; }

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

        protected override bool ToggleExtraValues()
        {
            if (YELLOW_FLAG <= note.value && note.value <= GREEN_FLAG)
            {
                _type = DrumsType.ProDrums;
                return IsFullyScanned();
            }
            return false;
        }
    }

    public class Midi_ProDrum : Midi_FourLaneDrum
    {
        public Midi_ProDrum() : base(DrumsType.ProDrums) { }
    }
}
