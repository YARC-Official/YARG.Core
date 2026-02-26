using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Core.Chart
{
    public static class LipsyncGenerator
    {
        private const double TRANSITION_TIME = 0.12;
        private const double HALF_TRANSITION = TRANSITION_TIME / 2;

        public static List<LipsyncEvent> GenerateFromLyrics(VocalsPart vocals)
        {
            var events = new List<LipsyncEvent>();
            
            foreach (var phrase in vocals.NotePhrases)
            {
                if (phrase.IsPercussion || phrase.Lyrics.Count == 0)
                    continue;

                for (int i = 0; i < phrase.Lyrics.Count; i++)
                {
                    var lyric = phrase.Lyrics[i];
                    var viseme = GetVisemeForLyric(lyric.Text);
                    
                    // Add viseme at lyric start (value normalized to 0-1)
                    events.Add(new LipsyncEvent(viseme, 1.0f, lyric.Time, lyric.Tick));
                    
                    // Reset the same viseme after lyric
                    var endTime = i < phrase.Lyrics.Count - 1 
                        ? phrase.Lyrics[i + 1].Time 
                        : phrase.Time + phrase.TimeLength;
                    
                    events.Add(new LipsyncEvent(viseme, 0f, 
                        endTime - HALF_TRANSITION, lyric.Tick));
                }
            }

            return events.OrderBy(e => e.Time).ToList();
        }

        private static LipsyncEvent.LipsyncType GetVisemeForLyric(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return LipsyncEvent.LipsyncType.Neutral_lo;

            var clean = text.ToLowerInvariant()
                .Replace("-", "")
                .Replace("=", "")
                .Replace("#", "")
                .Replace("^", "")
                .Replace("$", "")
                .Trim();

            if (clean.Length == 0)
                return LipsyncEvent.LipsyncType.Neutral_lo;

            // Simple vowel-based mapping
            var vowels = clean.Where(c => "aeiou".Contains(c)).ToArray();
            if (vowels.Length == 0)
            {
                // Consonant-heavy, use appropriate viseme
                if (clean.Any(c => "bpm".Contains(c)))
                    return LipsyncEvent.LipsyncType.Bump_lo;
                if (clean.Any(c => "fv".Contains(c)))
                    return LipsyncEvent.LipsyncType.Fave_lo;
                if (clean.Any(c => "td".Contains(c)))
                    return LipsyncEvent.LipsyncType.Told_lo;
                if (clean.Any(c => "sz".Contains(c)))
                    return LipsyncEvent.LipsyncType.Size_lo;
                if (clean.Any(c => "ckg".Contains(c)))
                    return LipsyncEvent.LipsyncType.Cage_lo;
                if (clean.Any(c => "rl".Contains(c)))
                    return LipsyncEvent.LipsyncType.Roar_lo;
                if (clean.Any(c => "w".Contains(c)))
                    return LipsyncEvent.LipsyncType.Wet_lo;
                if (clean.Any(c => "th".Contains(c)))
                    return LipsyncEvent.LipsyncType.Though_lo;
                
                return LipsyncEvent.LipsyncType.Neutral_lo;
            }

            // Map primary vowel to viseme
            var primaryVowel = vowels[0];
            return primaryVowel switch
            {
                'a' => LipsyncEvent.LipsyncType.Ox_lo,      // "ah" sound
                'e' => LipsyncEvent.LipsyncType.Cage_lo,    // "eh" sound
                'i' => LipsyncEvent.LipsyncType.Eat_lo,     // "ee" sound
                'o' => LipsyncEvent.LipsyncType.Oat_lo,     // "oh" sound
                'u' => LipsyncEvent.LipsyncType.Wet_lo,     // "oo" sound
                _ => LipsyncEvent.LipsyncType.Neutral_lo
            };
        }
    }
}
