using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A phrase within the lyrics track.
    /// </summary>
    public class LyricsPhrase : ICloneable<LyricsPhrase>
    {
        public Phrase Bounds { get; }

        public double Time       => Bounds.Time;
        public double TimeLength => Bounds.TimeLength;
        public double TimeEnd    => Bounds.TimeEnd;

        public uint Tick       => Bounds.Tick;
        public uint TickLength => Bounds.TickLength;
        public uint TickEnd    => Bounds.TickEnd;

        public List<LyricEvent> Lyrics { get; } = new();

        public LyricsPhrase(Phrase bounds, List<LyricEvent> lyrics)
        {
            Bounds = bounds;
            Lyrics = lyrics;
        }

        public LyricsPhrase(LyricsPhrase other)
            : this(other.Bounds.Clone(), other.Lyrics.Duplicate())
        {
        }

        public LyricsPhrase Clone()
        {
            return new(this);
        }
    }
}