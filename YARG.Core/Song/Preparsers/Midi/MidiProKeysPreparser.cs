namespace YARG.Core.Song
{
    public abstract class Midi_ProKeys : MidiInstrumentPreparser
    {
        private readonly bool[] lanes = new bool[25];
        private readonly int difficulty;

        protected Midi_ProKeys(int difficulty)
        {
            this.difficulty = difficulty;
        }

        protected override bool IsNote() { return 48 <= note.value && note.value <= 72; }

        protected override bool ParseLaneColor()
        {
            lanes[note.value - 48] = true;
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            if (!lanes[note.value - 48])
                return false;

            Validate(difficulty);
            return true;
        }
    }

    public class Midi_ProKeysX : Midi_ProKeys
    {
        public Midi_ProKeysX() : base(3) { }
    }

    public class Midi_ProKeysH : Midi_ProKeys
    {
        public Midi_ProKeysH() : base(2) { }
    }

    public class Midi_ProKeysM : Midi_ProKeys
    {
        public Midi_ProKeysM() : base(1) { }
    }

    public class Midi_ProKeysE : Midi_ProKeys
    {
        public Midi_ProKeysE() : base(0) { }
    }
}
