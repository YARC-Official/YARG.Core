﻿using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public class Midi_UnknownDrums : Midi_Drum
    {
        private DrumType _type = DrumType.FourLane;
        public DrumType Type { get { return _type; } }

        public Midi_UnknownDrums(DrumType type)
        {
            _type = type;
        }

        protected override bool IsFullyScanned() { return validations == 31 && _type != DrumType.FourLane; }
        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 60;
            int lane = LANEVALUES[noteValue];
            if (lane < 7)
            {
                notes[DIFFVALUES[noteValue], lane] = true;
                if (lane == 6)
                {
                    _type = DrumType.FiveLane;
                    return IsFullyScanned();
                }
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

        protected override bool ToggleExtraValues()
        {
            if (110 <= note.value && note.value <= 112)
            {
                _type = DrumType.FourPro;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
