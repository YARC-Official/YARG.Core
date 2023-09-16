namespace YARG.Core.Song
{
    public abstract class Midi_Drum_Preparser_Base : MidiInstrument_Common
    {
        private const int DOUBLE_BASS_NOTE = 95;
        private const int DOUBLE_BASS_INDEX = 1;
        private const int EXPERT_INDEX = 3;
        protected const int FIVELANE_MAX = 101;
        protected const int MAX_NUMPADS = 7;
        protected const int YELLOW_FLAG = 110;
        protected const int GREEN_FLAG = 112;

        protected Midi_Drum_Preparser_Base() { }

        protected static readonly int[] LANEINDICES = new int[] {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };

        protected readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, MAX_NUMPADS];

        protected override bool ProcessSpecialNote_ON()
        {
            if (note.value != DOUBLE_BASS_NOTE)
                return false;

            statuses[EXPERT_INDEX, DOUBLE_BASS_INDEX] = true;
            return true;
        }

        protected override bool ProcessSpecialNote_Off()
        {
            if (note.value != DOUBLE_BASS_NOTE)
                return false;

            if (statuses[EXPERT_INDEX, DOUBLE_BASS_INDEX])
                validations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
            return true;
        }
    }
}
