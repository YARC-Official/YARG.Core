using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A single part on a vocals track.
    /// </summary>
    public class VocalsPart
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

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            if (NotePhrases.Count > 0)
                totalFirstTick = Math.Min(NotePhrases[^1].GetFirstTick(), totalFirstTick);

            totalFirstTick = Math.Min(OtherPhrases.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(TextEvents.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            if (NotePhrases.Count > 0)
                totalLastTick = Math.Max(NotePhrases[^1].GetLastTick(), totalLastTick);

            totalLastTick = Math.Max(OtherPhrases.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TextEvents.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}