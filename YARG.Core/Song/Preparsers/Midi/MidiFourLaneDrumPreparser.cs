namespace YARG.Core.Song
{
    public class Midi_FourLaneDrum : Midi_Drum
    {
        private DrumType _type;
        public DrumType Type => _type;
        protected Midi_FourLaneDrum(DrumType type)
        {
            _type = type;
        }

        public Midi_FourLaneDrum() : this(DrumType.FOUR_LANE) { }

        protected override bool IsNote() { return 60 <= note.value && note.value <= 100; }

        protected override bool IsFullyScanned() { return validations == 31 && _type == DrumType.FOUR_PRO; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 6)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 6)
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        protected override bool ToggleExtraValues()
        {
            if (110 <= note.value && note.value <= 112)
            {
                _type = DrumType.FOUR_PRO;
                return IsFullyScanned();
            }
            return false;
        }
    }

    public class Midi_ProDrum : Midi_FourLaneDrum
    {
        public Midi_ProDrum() : base(DrumType.FOUR_PRO) { }
    }
}
