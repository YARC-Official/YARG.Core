namespace YARG.Core.Chart
{
    public static class InstrumentDifficultyExtensions
    {

        public static void ConvertToGuitarType(this InstrumentDifficulty<GuitarNote> difficulty, GuitarNoteType type)
        {
            foreach (var note in difficulty.Notes)
            {
                note.Type = type;
                foreach (var child in note.ChildNotes)
                {
                    child.Type = type;
                }
            }
        }

        public static void ConvertHoposToTaps(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            foreach (var note in difficulty.Notes)
            {
                if (note.Type != GuitarNoteType.Hopo)
                {
                    continue;
                }

                note.Type = GuitarNoteType.Tap;
                foreach (var child in note.ChildNotes)
                {
                    child.Type = GuitarNoteType.Tap;
                }
            }
        }

    }
}