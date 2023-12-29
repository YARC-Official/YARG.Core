using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lyric/percussion phrase on a vocals track.
    /// </summary>
    public class VocalsPhrase : ChartEvent, ICloneable<VocalsPhrase>
    {
        public VocalNote PhraseParentNote { get; }
        public List<TextEvent> Lyrics { get; } = new();

        public bool IsLyric => !PhraseParentNote.IsPercussion;
        public bool IsPercussion => PhraseParentNote.IsPercussion;

        public bool IsStarPower => PhraseParentNote.IsStarPower;

        public VocalsPhrase(double time, double timeLength, uint tick, uint tickLength,
            VocalNote phraseParentNote, List<TextEvent> lyrics)
            : base(time, timeLength, tick, tickLength)
        {
            if (!phraseParentNote.IsPhrase)
            {
                throw new InvalidOperationException(
                    "Attempted to create a vocals phrase out of a non-phrase vocals note!");
            }

            PhraseParentNote = phraseParentNote;
            Lyrics = lyrics;
        }

        public VocalsPhrase(VocalsPhrase other)
            : this(other.Time, other.TimeLength, other.Tick, other.TickLength,
                other.PhraseParentNote.CloneAsPhrase(), other.Lyrics.Duplicate())
        {
        }

        public VocalsPhrase Clone()
        {
            return new(this);
        }
    }
}