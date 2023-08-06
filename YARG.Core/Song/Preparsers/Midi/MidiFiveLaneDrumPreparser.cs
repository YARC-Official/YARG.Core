namespace YARG.Core.Song
{
    public class Midi_FiveLaneDrum : Midi_Drum
    {
        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override bool IsFullyScanned() { return validations == 31; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 7)
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
                if (lane < 7)
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }
}
