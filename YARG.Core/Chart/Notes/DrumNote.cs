namespace YARG.Core.Chart
{
    public class DrumNote : Note 
    {
        public int Pad { get; }

        public bool IsCymbal => (_flags & NoteFlags.Cymbal) != 0;

        public bool IsGhost  => (_flags & NoteFlags.DrumGhost) != 0;
        public bool IsAccent => (_flags & NoteFlags.DrumAccent) != 0;

        public DrumNote(int pad, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0) 
        {
            Pad = pad;
        }
    }
}