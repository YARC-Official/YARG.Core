using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;

namespace YARG.Core.Engine.Vocals
{
    /// Builds the note list that feeds the wait-countdown wheel for the Party Vocals
    /// prototype engines (Free Vocals and the multi-mic coordinator). Mirrors
    /// <see cref="VocalsEngine.BuildCountdownsFromAllParts"/> but drops percussion-only
    /// phrases so percussion stretches read as gaps to the countdown wheel instead of a
    /// continuous note stream. Kept out of the upstream VocalsEngine so the base engine's
    /// countdown methods stay byte-identical to upstream.
    internal static class PartyVocalsCountdownNotes
    {
        public static List<VocalNote> ExcludingPercussion(List<VocalsPart> allParts)
        {
            var allNotes = new List<VocalNote>();

            foreach (var part in allParts)
            {
                allNotes.AddRange(part.CloneAsInstrumentDifficulty().Notes
                    .Where(n => n.ChildNotes.Any(c => !c.IsPercussion)));
            }

            if (allParts.Count > 1)
            {
                // Sort combined list by Note time
                allNotes.Sort((a, b) => (int) (a.Tick - b.Tick));
            }

            return allNotes;
        }
    }
}
