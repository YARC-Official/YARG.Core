namespace YARG.Core.Engine
{
    public class SoloSection
    {

        public int NoteCount { get; }

        public int NotesHit { get; set; }

        public int SoloBonus { get; set; }

        public uint   StartTick { get; private set; }
        public uint   EndTick   { get; private set; }
        public double StartTime { get; private set; }
        public double EndTime   { get; private set; }

        public SoloSection(uint start, uint end, double startTime, double endTime, int noteCount)
        {
            StartTick = start;
            EndTick = end;
            StartTime = startTime;
            EndTime = endTime;
            NoteCount = noteCount;
        }

    }
}