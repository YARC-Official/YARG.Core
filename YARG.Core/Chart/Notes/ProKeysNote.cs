namespace YARG.Core.Chart
{
    public class ProKeysNote : Note<ProKeysNote>
    {
        public int Key          { get; }
        public int DisjointMask { get; }
        public int NoteMask     { get; private set; }

        public bool IsSustain => TickLength > 0;

        public ProKeysNote(int key, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Key = key;

            NoteMask = GetKeyMask(Key);
        }

        public ProKeysNote(ProKeysNote other) : base(other)
        {
            Key = other.Key;

            NoteMask = GetKeyMask(Key);
            DisjointMask = GetKeyMask(Key);
        }

        public override void AddChildNote(ProKeysNote note)
        {
            if ((NoteMask & GetKeyMask(note.Key)) != 0) return;

            base.AddChildNote(note);

            NoteMask |= GetKeyMask(note.Key);
        }

        protected override void CopyFlags(ProKeysNote other)
        {
        }

        protected override ProKeysNote CloneNote()
        {
            return new(this);
        }

        private static int GetKeyMask(int key)
        {
            return 1 << key;
        }
    }
}