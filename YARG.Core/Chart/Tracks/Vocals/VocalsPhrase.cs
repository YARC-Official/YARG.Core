using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lyric/percussion phrase on a vocals track.
    /// </summary>
    public class VocalsPhrase
    {
        private readonly VocalsPhraseFlags _flags;

        public VocalsPhraseType Type { get; }

        public ChartEvent Bounds { get; }

        public List<VocalNote> Notes  { get; } = new();
        public List<TextEvent> Lyrics { get; } = new();

        public bool IsLyric      => Type == VocalsPhraseType.Lyric;
        public bool IsPercussion => Type == VocalsPhraseType.Percussion;

        public bool IsStarPower => (_flags & VocalsPhraseFlags.StarPower) != 0;

        public VocalsPhrase(VocalsPhraseType type, ChartEvent bounds, VocalsPhraseFlags flags)
        {
            _flags = flags;

            Type = type;
            Bounds = bounds;
        }

        public VocalsPhrase(VocalsPhraseType type, ChartEvent bounds, VocalsPhraseFlags flags, List<VocalNote> notes,
            List<TextEvent> lyrics)
            : this(type, bounds, flags)
        {
            Notes = notes;
            Lyrics = lyrics;
        }

        public uint GetFirstTick()
        {
            // Events inside a phrase cannot exceed its bounds 
            return Bounds.Tick;
        }

        public uint GetLastTick()
        {
            // Events inside a phrase cannot exceed its bounds 
            return Bounds.TickEnd;
        }
    }

    public enum VocalsPhraseType
    {
        Lyric,
        Percussion,
    }

    public enum VocalsPhraseFlags
    {
        None = 0,

        StarPower = 1 << 0,
    }
}