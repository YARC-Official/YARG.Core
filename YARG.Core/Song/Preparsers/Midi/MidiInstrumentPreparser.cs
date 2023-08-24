namespace YARG.Core.Song
{
    public abstract class Midi_Instrument_Preparser : Midi_Preparser
    {
        private const int ALL_DIFFICULTIES = 15;
        protected const int DEFAULT_MIN = 60;
        protected const int DEFAULT_MAX = 100;
        protected const int NUM_DIFFICULTIES = 4;
        protected int validations;

        protected Midi_Instrument_Preparser() { }

        protected override bool ParseNote_ON()
        {
            if (ProcessSpecialNote_ON())
                return false;

            if (IsNote())
                return ParseLaneColor_ON();
            return ToggleExtraValues();
        }

        protected override bool ParseNote_Off()
        {
            return ProcessSpecialNote_Off() || (IsNote() && ParseLaneColor_Off());
        }

        protected abstract bool ParseLaneColor_ON();

        protected abstract bool ParseLaneColor_Off();

        protected virtual bool IsFullyScanned() { return validations == ALL_DIFFICULTIES; }

        protected virtual bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= DEFAULT_MAX; }

        protected virtual bool ProcessSpecialNote_ON() { return false; }

        protected virtual bool ProcessSpecialNote_Off() { return false; }

        protected virtual bool ToggleExtraValues() { return false; }

        protected void Validate(int diffIndex) { validations |= 1 << diffIndex; }
    }

    public abstract class MidiInstrument_Common : Midi_Instrument_Preparser
    {
        protected const int NOTES_PER_DIFFICULTY = 12;
        protected static readonly int[] DIFFVALUES = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        };

        protected bool[] difficultyTracker = new bool[NUM_DIFFICULTIES];
    }
}
