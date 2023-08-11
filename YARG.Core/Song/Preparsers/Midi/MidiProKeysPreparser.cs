namespace YARG.Core.Song
{
    public abstract class Midi_ProKeys : MidiInstrumentPreparser
    {
        private const int NOTES_PER_DIFFICULTY = 25;
        private const int PROKEYS_MIN = 48;
        private const int PROKEYS_MAX = 72;

        private readonly bool[] statuses = new bool[NOTES_PER_DIFFICULTY];
        private readonly DifficultyMask difficulty;

        protected Midi_ProKeys(DifficultyMask difficulty)
        {
            this.difficulty = difficulty;
        }

        protected override bool IsNote() { return PROKEYS_MIN <= note.value && note.value <= PROKEYS_MAX; }

        protected override bool ParseLaneColor()
        {
            statuses[note.value - PROKEYS_MIN] = true;
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            if (!statuses[note.value - PROKEYS_MIN])
                return false;

            validations |= (int) difficulty;
            return true;
        }
    }

    public class Midi_ProKeysX : Midi_ProKeys
    {
        public Midi_ProKeysX() : base(DifficultyMask.Expert) { }
    }

    public class Midi_ProKeysH : Midi_ProKeys
    {
        public Midi_ProKeysH() : base(DifficultyMask.Hard) { }
    }

    public class Midi_ProKeysM : Midi_ProKeys
    {
        public Midi_ProKeysM() : base(DifficultyMask.Medium) { }
    }

    public class Midi_ProKeysE : Midi_ProKeys
    {
        public Midi_ProKeysE() : base(DifficultyMask.Easy) { }
    }
}
