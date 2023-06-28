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
    }
}