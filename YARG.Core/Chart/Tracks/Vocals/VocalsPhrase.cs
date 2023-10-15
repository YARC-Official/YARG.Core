using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lyric/percussion phrase on a vocals track.
    /// </summary>
    public class VocalsPhrase : ICloneable<VocalsPhrase>
    {
        public Phrase Bounds { get; }

        public double Time       => Bounds.Time;
        public double TimeLength => Bounds.TimeLength;
        public double TimeEnd    => Bounds.TimeEnd;

        public uint Tick       => Bounds.Tick;
        public uint TickLength => Bounds.TickLength;
        public uint TickEnd    => Bounds.TickEnd;

        public VocalNote PhraseParentNote { get; }
        public List<TextEvent> Lyrics { get; } = new();

        public bool IsLyric => !PhraseParentNote.IsPercussion;
        public bool IsPercussion => PhraseParentNote.IsPercussion;

        public bool IsStarPower => PhraseParentNote.IsStarPower;


        public VocalsPhrase(Phrase bounds, VocalNote phraseParentNote, List<TextEvent> lyrics)
        {
            Bounds = bounds;

            if (!phraseParentNote.IsPhrase)
            {
                throw new InvalidOperationException(
                    "Attempted to create a vocals phrase out of a non-phrase vocals note!");
            }

            PhraseParentNote = phraseParentNote;
            Lyrics = lyrics;
        }

        public VocalsPhrase(VocalsPhrase other)
            : this(other.Bounds.Clone(), other.PhraseParentNote.Clone(), other.Lyrics.Duplicate())
        {
        }

        public VocalsPhrase Clone()
        {
            return new(this);
        }
    }
}