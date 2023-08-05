namespace YARG.Core.Song
{
    public abstract class Midi_Drum : MidiInstrument_Common
    {
        internal static readonly int[] LANEVALUES = new int[] {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };

        protected readonly bool[,] notes = new bool[4, 7];

        protected override bool ProcessSpecialNote()
        {
            if (note.value != 95)
                return false;

            notes[3, 1] = true;
            return true;
        }

        protected override bool ProcessSpecialNote_Off()
        {
            if (note.value != 95)
                return false;

            if (notes[3, 1])
                validations |= 24;
            return true;
        }
    }
}
