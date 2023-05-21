namespace YARG.Core.Chart
{
    public class DrumNote : Note 
    {
        public int Pad { get; }

        public bool IsCymbal => (_flags & NoteFlags.Cymbal) != 0;

        public bool IsGhost  => (_flags & NoteFlags.DrumGhost) != 0;
        public bool IsAccent => (_flags & NoteFlags.DrumAccent) != 0;

        public DrumNote(Note previousNote, double time, uint tick, int pad, NoteFlags flags)
            : base(previousNote, time, 0, tick, 0, flags) 
        {
            Pad = pad;
        }
    }
}