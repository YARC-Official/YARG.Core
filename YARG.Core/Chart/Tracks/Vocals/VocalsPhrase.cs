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
        private readonly VocalsPhraseFlags _flags;

        public VocalsPhraseType Type { get; }

        public Phrase Bounds { get; }

        public double Time       => Bounds.Time;
        public double TimeLength => Bounds.TimeLength;
        public double TimeEnd    => Bounds.TimeEnd;

        public uint Tick       => Bounds.Tick;
        public uint TickLength => Bounds.TickLength;
        public uint TickEnd    => Bounds.TickEnd;

        public List<VocalNote> Notes  { get; } = new();
        public List<TextEvent> Lyrics { get; } = new();

        public bool IsLyric      => Type == VocalsPhraseType.Lyric;
        public bool IsPercussion => Type == VocalsPhraseType.Percussion;

        public bool IsStarPower => (_flags & VocalsPhraseFlags.StarPower) != 0;

        public VocalsPhrase(VocalsPhraseType type, Phrase bounds, VocalsPhraseFlags flags)
        {
            _flags = flags;

            Type = type;
            Bounds = bounds;
        }

        public VocalsPhrase(VocalsPhraseType type, Phrase bounds, VocalsPhraseFlags flags, List<VocalNote> notes,
            List<TextEvent> lyrics)
            : this(type, bounds, flags)
        {
            Notes = notes;
            Lyrics = lyrics;
        }

        public VocalsPhrase(VocalsPhrase other)
            : this(other.Type, other.Bounds.Clone(), other._flags,
                // NOTE: Does not use DuplicateNotes(), as vocals notes are not currently linked together
                // TODO: Should we make that happen?
                other.Notes.Duplicate(), other.Lyrics.Duplicate())
        {
        }

        public VocalsPhrase Clone()
        {
            return new(this);
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