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

        public VocalsPhrase(NoteFlags phraseFlags, Phrase bounds)
        {
            Bounds = bounds;

            PhraseParentNote = new VocalNote(phraseFlags, bounds.Time,
                bounds.TimeLength, bounds.Tick, bounds.TickLength);
        }

        public VocalsPhrase(Phrase bounds, VocalNote phraseParentNote, List<TextEvent> lyrics) : this(bounds)
        {
            if (!phraseParentNote.IsPhrase)
            {
                throw new InvalidOperationException(
                    "Attempted to create a vocals phrase out of a non-phrase vocals note!");
            }

            PhraseParentNote = phraseParentNote;
            Lyrics = lyrics;
        }

        // public VocalsPhrase(VocalsPhrase other)
        //     : this(other.Type, other.Bounds.Clone(), other._flags,
        //         // NOTE: Does not use DuplicateNotes(), as vocals notes are not currently linked together
        //         // TODO: Should we make that happen?
        //         other.Notes.Duplicate(), other.Lyrics.Duplicate())
        // {
        // }

        public VocalsPhrase Clone()
        {
            return new(this);
        }
    }
}