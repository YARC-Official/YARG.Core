using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Flags for lyric events.
    /// </summary>
    [Flags]
    public enum LyricFlags
    {
        None = 0,

        JoinWithNext = 1 << 0,
        HarmonyHidden = 1 << 1,
        StaticShift = 1 << 1,
    }

    /// <summary>
    /// A text event used for chart lyrics.
    /// </summary>
    public class LyricEvent : ChartEvent, ICloneable<LyricEvent>
    {
        private readonly LyricFlags _flags;

        public string Text { get; }

        public LyricFlags Flags => _flags;

        public bool JoinWithNext => (_flags & LyricFlags.JoinWithNext) != 0;
        public bool HarmonyHidden => (_flags & LyricFlags.HarmonyHidden) != 0;
        public bool StaticShift => (_flags & LyricFlags.StaticShift) != 0;

        public LyricEvent(LyricFlags flags, string text, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            _flags = flags;
            Text = text;
        }

        public LyricEvent(LyricEvent other) : base(other)
        {
            _flags = other._flags;
            Text = other.Text;
        }

        public LyricEvent Clone()
        {
            return new(this);
        }

        public override string ToString()
        {
            return $"Lyric event '{Text}' at {Time}s ({Tick}t) with flags {_flags}";
        }
    }
}