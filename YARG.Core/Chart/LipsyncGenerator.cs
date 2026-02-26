using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YARG.Core.Chart
{
    public static class LipsyncGenerator
    {
        private const double TRANSITION_TIME = 0.12;
        private const double HALF_TRANSITION = TRANSITION_TIME / 2;
        
        private static Dictionary<string, string[]> _cmuDict;

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

            // Try CMU dictionary first
            if (TryGetPhonemes(clean, out var phonemes) && phonemes.Length > 0)
            {
                // Find primary vowel (first vowel phoneme)
                foreach (var phoneme in phonemes)
                {
                    var viseme = PhonemeToViseme(phoneme);
                    if (viseme != LipsyncEvent.LipsyncType.Neutral_lo)
                        return viseme;
                }
            }

            // Fallback to simple vowel-based mapping
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
                'a' => LipsyncEvent.LipsyncType.Ox_lo,
                'e' => LipsyncEvent.LipsyncType.Cage_lo,
                'i' => LipsyncEvent.LipsyncType.Eat_lo,
                'o' => LipsyncEvent.LipsyncType.Oat_lo,
                'u' => LipsyncEvent.LipsyncType.Wet_lo,
                _ => LipsyncEvent.LipsyncType.Neutral_lo
            };
        }

        private static bool TryGetPhonemes(string word, out string[] phonemes)
        {
            if (_cmuDict == null)
                LoadCMUDict();

            var key = word.ToUpperInvariant();
            return _cmuDict.TryGetValue(key, out phonemes);
        }

        private static void LoadCMUDict()
        {
            _cmuDict = new Dictionary<string, string[]>();
            
            var assembly = typeof(LipsyncGenerator).Assembly;
            var resourceName = "YARG.Core.Resources.cmudict.txt";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return;
                    
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith(";;;") || string.IsNullOrWhiteSpace(line))
                            continue;

                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2)
                            continue;

                        var word = parts[0];
                        var parenIdx = word.IndexOf('(');
                        if (parenIdx > 0)
                            word = word.Substring(0, parenIdx);

                        var phonemes = parts.Skip(1).Select(p => p.TrimEnd('0', '1', '2')).ToArray();
                        
                        if (!_cmuDict.ContainsKey(word))
                            _cmuDict[word] = phonemes;
                    }
                }
            }
        }

        private static LipsyncEvent.LipsyncType PhonemeToViseme(string phoneme)
        {
            return phoneme switch
            {
                "AA" => LipsyncEvent.LipsyncType.Ox_lo,
                "AE" => LipsyncEvent.LipsyncType.Cage_lo,
                "AH" => LipsyncEvent.LipsyncType.If_lo,
                "AO" => LipsyncEvent.LipsyncType.Earth_lo,
                "AW" => LipsyncEvent.LipsyncType.If_lo,
                "AY" => LipsyncEvent.LipsyncType.Ox_lo,
                "EH" => LipsyncEvent.LipsyncType.Cage_lo,
                "ER" => LipsyncEvent.LipsyncType.Church_lo,
                "EY" => LipsyncEvent.LipsyncType.Cage_lo,
                "IH" => LipsyncEvent.LipsyncType.If_lo,
                "IY" => LipsyncEvent.LipsyncType.Eat_lo,
                "OW" => LipsyncEvent.LipsyncType.Earth_lo,
                "OY" => LipsyncEvent.LipsyncType.Oat_lo,
                "UH" => LipsyncEvent.LipsyncType.Though_lo,
                "UW" => LipsyncEvent.LipsyncType.Wet_lo,
                "B" or "P" or "M" => LipsyncEvent.LipsyncType.Bump_lo,
                "F" or "V" => LipsyncEvent.LipsyncType.Fave_lo,
                "TH" or "DH" => LipsyncEvent.LipsyncType.Though_lo,
                "S" or "Z" => LipsyncEvent.LipsyncType.Size_lo,
                "T" or "D" or "N" or "L" => LipsyncEvent.LipsyncType.Told_lo,
                "SH" or "ZH" or "CH" or "JH" => LipsyncEvent.LipsyncType.Church_lo,
                "K" or "G" or "NG" => LipsyncEvent.LipsyncType.Cage_lo,
                "R" => LipsyncEvent.LipsyncType.Roar_lo,
                "W" => LipsyncEvent.LipsyncType.Wet_lo,
                "Y" => LipsyncEvent.LipsyncType.Eat_lo,
                "HH" => LipsyncEvent.LipsyncType.If_lo,
                _ => LipsyncEvent.LipsyncType.Neutral_lo
            };
        }
    }
}
