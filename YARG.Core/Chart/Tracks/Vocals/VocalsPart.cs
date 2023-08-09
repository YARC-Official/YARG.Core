using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A single part on a vocals track.
    /// </summary>
    public class VocalsPart : ICloneable<VocalsPart>
    {
        public List<VocalsPhrase> NotePhrases { get; } = new();
        public List<Phrase> OtherPhrases { get; } = new();
        public List<TextEvent> TextEvents { get; } = new();

        public VocalsPart(List<VocalsPhrase> notePhrases, List<Phrase> otherPhrases, List<TextEvent> text)
        {
            NotePhrases = notePhrases;
            OtherPhrases = otherPhrases;
            TextEvents = text;
        }

        public VocalsPart(VocalsPart other)
            : this(other.NotePhrases.Duplicate(), other.OtherPhrases.Duplicate(), other.TextEvents.Duplicate())
        {
        }

        public double GetStartTime()
        {
            double totalStartTime = 0;

            if (NotePhrases.Count > 0)
                totalStartTime = Math.Min(NotePhrases[^1].Time, totalStartTime);

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

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            if (NotePhrases.Count > 0)
                totalFirstTick = Math.Min(NotePhrases[^1].Tick, totalFirstTick);

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

        public bool IsOccupied()
        {
            return NotePhrases.Count > 0 || OtherPhrases.Count > 0 || TextEvents.Count > 0;
        }

        public VocalsPart Clone()
        {
            return new(this);
        }
    }
}