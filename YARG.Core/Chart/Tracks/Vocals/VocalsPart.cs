using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A single part on a vocals track.
    /// </summary>
    public class VocalsPart : ICloneable<VocalsPart>
    {
        public readonly bool IsHarmony;

        public List<VocalsPhrase> NotePhrases { get; } = new();
        public List<VocalsPhrase> StaticLyricPhrases { get; } = new();
        public List<Phrase> OtherPhrases { get; } = new();
        public List<TextEvent> TextEvents { get; } = new();

        /// <summary>
        /// Whether or not this part contains any data.
        /// </summary>
        public bool IsEmpty => NotePhrases.Count == 0 && OtherPhrases.Count == 0 && TextEvents.Count == 0;

        public VocalsPart(bool isHarmony, List<VocalsPhrase> notePhrases, List<VocalsPhrase> staticLyricPhrases,
            List<Phrase> otherPhrases, List<TextEvent> text)
        {
            IsHarmony = isHarmony;
            NotePhrases = notePhrases;
            StaticLyricPhrases = staticLyricPhrases;
            OtherPhrases = otherPhrases;
            TextEvents = text;
        }

        public VocalsPart(VocalsPart other)
            : this(other.IsHarmony, other.NotePhrases.Duplicate(), other.StaticLyricPhrases.Duplicate(),
                other.OtherPhrases.Duplicate(), other.TextEvents.Duplicate())
        {
        }

        /// <summary>
        /// Gets the start time of the first event in this vocal part
        /// </summary>
        /// <returns>double</returns>
        /// <remarks>This returns double.MaxValue if there are no events</remarks>
        public double GetStartTime()
        {
            double totalStartTime = double.MaxValue;

            if (NotePhrases.Count > 0)
                totalStartTime = Math.Min(NotePhrases[0].Time, totalStartTime);

            totalStartTime = Math.Min(OtherPhrases.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(TextEvents.GetStartTime(), totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;

            if (NotePhrases.Count > 0)
                totalEndTime = Math.Max(NotePhrases[^1].TimeEnd, totalEndTime);

            totalEndTime = Math.Max(OtherPhrases.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(TextEvents.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public double GetFirstNoteStartTime()
        {
            if (NotePhrases.Count > 0)
            {
                return NotePhrases[0].Time;
            }

            return double.MaxValue;
        }

        public double GetLastNoteEndTime()
        {
            if (NotePhrases.Count > 0)
            {
                return NotePhrases[^1].TimeEnd;
            }

            // Not sure if we should be returning 0 or MaxValue or something else.
            // 0 should be fine given what this is used for, but it feels somehow wrong
            return 0;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            if (NotePhrases.Count > 0)
                totalFirstTick = Math.Min(NotePhrases[0].Tick, totalFirstTick);

            totalFirstTick = Math.Min(OtherPhrases.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(TextEvents.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            if (NotePhrases.Count > 0)
                totalLastTick = Math.Max(NotePhrases[^1].TickEnd, totalLastTick);

            totalLastTick = Math.Max(OtherPhrases.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TextEvents.GetLastTick(), totalLastTick);

            return totalLastTick;
        }

        public InstrumentDifficulty<VocalNote> CloneAsInstrumentDifficulty()
        {
            // Deep-clone the phrase notes: multiple players can sing the same part, and
            // each engine mutates hit/miss state on its notes. Sharing instances would
            // leak one player's hit/miss state into every other engine on this part.
            var vocalNotes = NotePhrases.Select(i => i.PhraseParentNote.Clone()).ToList();
            var instrument = IsHarmony ? Instrument.Harmony : Instrument.Vocals;

            var diff = new InstrumentDifficulty<VocalNote>(instrument, Difficulty.Expert,
                vocalNotes, new(OtherPhrases), new(TextEvents));

            return diff;
        }

        public VocalsPart Clone()
        {
            return new VocalsPart(this);
        }

        public void TrimToTickRange(uint tickStart, uint tickEnd)
        {
            NotePhrases.RemoveAll(phrase => !IsVocalPhraseInTickRange(phrase, tickStart, tickEnd));
            StaticLyricPhrases.RemoveAll(phrase => !IsVocalPhraseInTickRange(phrase, tickStart, tickEnd));
            OtherPhrases.RemoveAll(phrase => !IsTickInRange(phrase.Tick, tickStart, tickEnd));
            TextEvents.RemoveAll(text => !IsTickInRange(text.Tick, tickStart, tickEnd));
        }

        private static bool IsVocalPhraseInTickRange(VocalsPhrase phrase, uint tickStart, uint tickEnd)
        {
            if (IsTickInRange(phrase.Tick, tickStart, tickEnd))
            {
                return true;
            }

            if (phrase.PhraseParentNote.ChildNotes.Count == 0 && phrase.Lyrics.Count == 0)
            {
                return false;
            }

            if (phrase.PhraseParentNote.ChildNotes.Any(note => !IsNoteInRange(note, tickStart, tickEnd)))
            {
                return false;
            }

            return phrase.Lyrics.All(lyric => IsTickInRange(lyric.Tick, tickStart, tickEnd));
        }

        private static bool IsNoteInRange(VocalNote note, uint tickStart, uint tickEnd)
        {
            return note.Tick >= tickStart && note.TotalTickEnd <= tickEnd;
        }

        private static bool IsTickInRange(uint tick, uint tickStart, uint tickEnd)
        {
            return tick >= tickStart && tick < tickEnd;
        }
    }
}