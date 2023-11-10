using YARG.Core.IO;

namespace YARG.Core.Song
{
    public abstract class Midi_Instrument_Preparser : Midi_Preparser
    {
        protected const DifficultyMask ALL_DIFFICULTIES = DifficultyMask.Easy | DifficultyMask.Medium | DifficultyMask.Hard | DifficultyMask.Expert;
        protected const DifficultyMask ALL_DIFFICULTIES_PLUS = ALL_DIFFICULTIES | DifficultyMask.ExpertPlus;

        protected static readonly DifficultyMask[] DIFFINDEX_TO_DIFF = new DifficultyMask[NUM_DIFFICULTIES] {
            DifficultyMask.Easy, DifficultyMask.Medium, DifficultyMask.Hard, DifficultyMask.Expert
        };

        protected const int DEFAULT_MIN = 60;
        protected const int DEFAULT_MAX = 100;
        protected const int NUM_DIFFICULTIES = 4;

        protected DifficultyMask validations;

        protected Midi_Instrument_Preparser() { }

        protected override bool ParseNote_ON(YARGMidiTrack track)
        {
            if (ProcessSpecialNote_ON(track))
                return false;

            if (IsNote())
                return ParseLaneColor_ON(track);
            return ToggleExtraValues(track);
        }

        protected override bool ParseNote_Off(YARGMidiTrack track)
        {
            if (ProcessSpecialNote_Off(track))
                return IsFullyScanned();

            return IsNote() && ParseLaneColor_Off(track);
        }

        protected abstract bool ParseLaneColor_ON(YARGMidiTrack track);

        protected abstract bool ParseLaneColor_Off(YARGMidiTrack track);

        protected virtual bool IsFullyScanned() { return validations == ALL_DIFFICULTIES; }

        protected virtual bool IsNote() { return DEFAULT_MIN <= note.value && note.value <= DEFAULT_MAX; }

        protected virtual bool ProcessSpecialNote_ON(YARGMidiTrack track) { return false; }

        protected virtual bool ProcessSpecialNote_Off(YARGMidiTrack track) { return false; }

        protected virtual bool ToggleExtraValues(YARGMidiTrack track) { return false; }

        protected void Validate(int diffIndex) { validations |= DIFFINDEX_TO_DIFF[diffIndex]; }
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
