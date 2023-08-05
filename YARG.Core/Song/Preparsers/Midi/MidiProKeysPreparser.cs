namespace YARG.Core.Song
{
    public abstract class Midi_ProKeys : MidiInstrument
    {
        protected readonly bool[] lanes = new bool[25];

        protected override bool IsNote() { return 48 <= note.value && note.value <= 72; }

        protected override bool ParseLaneColor()
        {
            lanes[note.value - 48] = true;
            return false;
        }
    }

    public class Midi_ProKeysX : Midi_ProKeys
    {
        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            Validate(3);
            return true;
        }
    }

    public class Midi_ProKeysH : Midi_ProKeys
    {
        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            Validate(2);
            return true;
        }
    }

    public class Midi_ProKeysM : Midi_ProKeys
    {
        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            Validate(1);
            return true;
        }
    }

    public class Midi_ProKeysE : Midi_ProKeys
    {
        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            Validate(0);
            return true;
        }
    }
}
