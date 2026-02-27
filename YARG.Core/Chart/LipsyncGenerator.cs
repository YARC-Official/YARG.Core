using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    public static class LipsyncGenerator
    {
        private const double TRANSITION_TIME = 0.12;
        private const double HALF_TRANSITION = TRANSITION_TIME / 2;
        private const int TRANSITION_STEPS = 4; // 30fps * 0.12s = ~4 steps
        private const float VISEME_WEIGHT = 140f / 255f; // Match onyx weight
        
        private static Dictionary<string, string[]> _cmuDict;

        public static List<LipsyncEvent> GenerateFromLyrics(VocalsPart vocals)
        {
            var events = new List<LipsyncEvent>();
            var defaultViseme = LipsyncEvent.LipsyncType.Neutral_lo;
            
            var random = new Random();
            var nextBlinkTime = 2.0 + random.NextDouble() * 3.0; // First blink between 2-5s
            
            foreach (var phrase in vocals.NotePhrases)
            {
                if (phrase.IsPercussion || phrase.Lyrics.Count == 0)
                    continue;

                for (int i = 0; i < phrase.Lyrics.Count; i++)
                {
                    var lyric = phrase.Lyrics[i];
                    
                    // Add blinks if enough time has passed
                    while (nextBlinkTime < lyric.Time)
                    {
                        events.Add(new LipsyncEvent(LipsyncEvent.LipsyncType.Blink, 1.0f, nextBlinkTime, 0));
                        events.Add(new LipsyncEvent(LipsyncEvent.LipsyncType.Blink, 0f, nextBlinkTime + 0.15, 0));
                        nextBlinkTime += 2.0 + random.NextDouble() * 4.0; // Next blink in 2-6s
                    }
                    
                    var syllable = GetSyllableForLyric(lyric.Text);
                    
                    YargLogger.LogFormatTrace("Lyric '{0}' at tick {1} -> Initial: [{2}], Vowel: {3}, VowelEnd: {4}, Final: [{5}]",
                        (object)lyric.Text, (object)lyric.Tick,
                        (object)string.Join(", ", syllable.Initial),
                        (object)syllable.VowelMain,
                        (object)(syllable.VowelEnd?.ToString() ?? "none"),
                        (object)string.Join(", ", syllable.Final));
                    
                    var endTime = i < phrase.Lyrics.Count - 1 
                        ? phrase.Lyrics[i + 1].Time 
                        : phrase.Time + phrase.TimeLength;
                    
                    var duration = endTime - lyric.Time;
                    var initialBack = Math.Min(HALF_TRANSITION, duration / 2);
                    var finalFront = Math.Min(HALF_TRANSITION, duration / 2);
                    
                    var startTime = lyric.Time;
                    var eventCountBefore = events.Count;
                    
                    // Transition to initial consonants or vowel
                    if (syllable.Initial.Count > 0)
                    {
                        AddTransition(events, startTime, initialBack, 
                            defaultViseme, syllable.Initial, lyric.Tick);
                        startTime += initialBack;
                    }
                    
                    // Hold vowel (with diphthong if present)
                    var vowelDuration = duration - initialBack - finalFront;
                    if (syllable.VowelEnd.HasValue)
                    {
                        // Diphthong: transition from main to end vowel
                        AddDiphthong(events, startTime, vowelDuration,
                            syllable.VowelMain, syllable.VowelEnd.Value, lyric.Tick);
                    }
                    else
                    {
                        events.Add(new LipsyncEvent(syllable.VowelMain, VISEME_WEIGHT, startTime, lyric.Tick));
                    }
                    startTime += vowelDuration;
                    
                    // Transition through final consonants then close
                    if (syllable.Final.Count > 0)
                    {
                        AddTransition(events, startTime, finalFront,
                            syllable.VowelEnd ?? syllable.VowelMain, syllable.Final, lyric.Tick);
                        startTime += finalFront;
                    }
                    
                    // Close mouth
                    var lastViseme = syllable.Final.Count > 0 ? syllable.Final.Last() : 
                                     (syllable.VowelEnd ?? syllable.VowelMain);
                    events.Add(new LipsyncEvent(lastViseme, 0f, startTime, lyric.Tick));
                    
                    var eventCount = events.Count - eventCountBefore;
                    YargLogger.LogFormatTrace("  Generated {0} lipsync events for lyric '{1}'", 
                        (object)eventCount, (object)lyric.Text);
                }
            }

            return events.OrderBy(e => e.Time).ToList();
        }

        private static void AddTransition(List<LipsyncEvent> events, double startTime, double duration,
            LipsyncEvent.LipsyncType from, List<LipsyncEvent.LipsyncType> toSequence, uint tick)
        {
            if (toSequence.Count == 0) return;
            
            var segmentDuration = duration / (toSequence.Count + 1);
            var currentTime = startTime;
            var current = from;
            
            foreach (var target in toSequence)
            {
                AddSmoothTransition(events, currentTime, segmentDuration, current, target, tick);
                currentTime += segmentDuration;
                current = target;
            }
        }

        private static void AddSmoothTransition(List<LipsyncEvent> events, double startTime, double duration,
            LipsyncEvent.LipsyncType from, LipsyncEvent.LipsyncType to, uint tick)
        {
            if (duration <= 0)
            {
                events.Add(new LipsyncEvent(to, VISEME_WEIGHT, startTime, tick));
                return;
            }
            
            var stepDuration = duration / TRANSITION_STEPS;
            for (int i = 0; i < TRANSITION_STEPS; i++)
            {
                var t = (float)i / TRANSITION_STEPS;
                var time = startTime + i * stepDuration;
                
                // Linear interpolation between visemes
                events.Add(new LipsyncEvent(from, VISEME_WEIGHT * (1 - t), time, tick));
                events.Add(new LipsyncEvent(to, VISEME_WEIGHT * t, time, tick));
            }
            events.Add(new LipsyncEvent(to, VISEME_WEIGHT, startTime + duration, tick));
        }

        private static void AddDiphthong(List<LipsyncEvent> events, double startTime, double duration,
            LipsyncEvent.LipsyncType main, LipsyncEvent.LipsyncType end, uint tick)
        {
            var transitionStart = startTime + duration * 0.6; // Start transition 60% through
            var transitionDuration = duration * 0.4;
            
            events.Add(new LipsyncEvent(main, VISEME_WEIGHT, startTime, tick));
            
            var stepDuration = transitionDuration / TRANSITION_STEPS;
            for (int i = 1; i <= TRANSITION_STEPS; i++)
            {
                var t = (float)i / TRANSITION_STEPS;
                t = EaseInExpo(t); // Use exponential easing for diphthongs
                var time = transitionStart + i * stepDuration;
                
                events.Add(new LipsyncEvent(main, VISEME_WEIGHT * (1 - t), time, tick));
                events.Add(new LipsyncEvent(end, VISEME_WEIGHT * t, time, tick));
            }
        }

        private static float EaseInExpo(float t)
        {
            return t == 0 ? 0 : (float)Math.Pow(2, 10 * t - 10);
        }

        private struct Syllable
        {
            public List<LipsyncEvent.LipsyncType> Initial;
            public LipsyncEvent.LipsyncType VowelMain;
            public LipsyncEvent.LipsyncType? VowelEnd;
            public List<LipsyncEvent.LipsyncType> Final;
        }

        private static Syllable GetSyllableForLyric(string text)
        {
            var syllable = new Syllable
            {
                Initial = new List<LipsyncEvent.LipsyncType>(),
                VowelMain = LipsyncEvent.LipsyncType.Neutral_lo,
                VowelEnd = null,
                Final = new List<LipsyncEvent.LipsyncType>()
            };

            if (string.IsNullOrWhiteSpace(text))
                return syllable;

            var clean = text.ToLowerInvariant()
                .Replace("-", "").Replace("=", "").Replace("#", "")
                .Replace("^", "").Replace("$", "").Trim();

            if (clean.Length == 0)
                return syllable;

            // Try CMU dictionary
            if (TryGetPhonemes(clean, out var phonemes) && phonemes.Length > 0)
            {
                return PhonemesToSyllable(phonemes);
            }

            // Fallback to simple mapping
            return SimpleSyllable(clean);
        }

        private static Syllable PhonemesToSyllable(string[] phonemes)
        {
            var syllable = new Syllable
            {
                Initial = new List<LipsyncEvent.LipsyncType>(),
                VowelMain = LipsyncEvent.LipsyncType.Neutral_lo,
                VowelEnd = null,
                Final = new List<LipsyncEvent.LipsyncType>()
            };

            bool foundVowel = false;
            
            YargLogger.LogFormatTrace("  Phonemes: [{0}]", string.Join(", ", phonemes));
            
            foreach (var phoneme in phonemes)
            {
                var (viseme, isDiphthong, diphthongEnd) = PhonemeToViseme(phoneme);
                
                if (viseme == LipsyncEvent.LipsyncType.Neutral_lo)
                    continue;
                
                if (IsVowelPhoneme(phoneme))
                {
                    if (!foundVowel)
                    {
                        syllable.VowelMain = viseme;
                        if (isDiphthong)
                            syllable.VowelEnd = diphthongEnd;
                        foundVowel = true;
                    }
                }
                else
                {
                    if (!foundVowel)
                        syllable.Initial.Add(viseme);
                    else
                        syllable.Final.Add(viseme);
                }
            }

            return syllable;
        }

        private static Syllable SimpleSyllable(string text)
        {
            var syllable = new Syllable
            {
                Initial = new List<LipsyncEvent.LipsyncType>(),
                VowelMain = LipsyncEvent.LipsyncType.If_lo,
                VowelEnd = null,
                Final = new List<LipsyncEvent.LipsyncType>()
            };

            var vowels = text.Where(c => "aeiou".Contains(c)).ToArray();
            if (vowels.Length > 0)
            {
                syllable.VowelMain = vowels[0] switch
                {
                    'a' => LipsyncEvent.LipsyncType.Ox_lo,
                    'e' => LipsyncEvent.LipsyncType.Cage_lo,
                    'i' => LipsyncEvent.LipsyncType.Eat_lo,
                    'o' => LipsyncEvent.LipsyncType.Oat_lo,
                    'u' => LipsyncEvent.LipsyncType.Wet_lo,
                    _ => LipsyncEvent.LipsyncType.If_lo
                };
            }

            return syllable;
        }

        private static bool IsVowelPhoneme(string phoneme)
        {
            return phoneme switch
            {
                "AA" or "AE" or "AH" or "AO" or "AW" or "AY" or
                "EH" or "ER" or "EY" or "IH" or "IY" or "OW" or
                "OY" or "UH" or "UW" => true,
                _ => false
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

        private static (LipsyncEvent.LipsyncType viseme, bool isDiphthong, LipsyncEvent.LipsyncType? diphthongEnd) 
            PhonemeToViseme(string phoneme)
        {
            return phoneme switch
            {
                // Vowels
                "AA" => (LipsyncEvent.LipsyncType.Ox_lo, false, null),
                "AE" => (LipsyncEvent.LipsyncType.Cage_lo, false, null),
                "AH" => (LipsyncEvent.LipsyncType.If_lo, false, null),
                "AO" => (LipsyncEvent.LipsyncType.Earth_lo, false, null),
                "EH" => (LipsyncEvent.LipsyncType.Cage_lo, false, null),
                "ER" => (LipsyncEvent.LipsyncType.Church_lo, false, null),
                "IH" => (LipsyncEvent.LipsyncType.If_lo, false, null),
                "IY" => (LipsyncEvent.LipsyncType.Eat_lo, false, null),
                "UH" => (LipsyncEvent.LipsyncType.Though_lo, false, null),
                "UW" => (LipsyncEvent.LipsyncType.Wet_lo, false, null),
                
                // Diphthongs
                "AY" => (LipsyncEvent.LipsyncType.Ox_lo, true, LipsyncEvent.LipsyncType.If_lo),
                "EY" => (LipsyncEvent.LipsyncType.Cage_lo, true, LipsyncEvent.LipsyncType.If_lo),
                "OW" => (LipsyncEvent.LipsyncType.Oat_lo, true, LipsyncEvent.LipsyncType.Wet_lo),
                "AW" => (LipsyncEvent.LipsyncType.Ox_lo, true, LipsyncEvent.LipsyncType.Wet_lo),
                "OY" => (LipsyncEvent.LipsyncType.Oat_lo, true, LipsyncEvent.LipsyncType.If_lo),
                
                // Consonants
                "B" or "P" or "M" => (LipsyncEvent.LipsyncType.Bump_lo, false, null),
                "F" or "V" => (LipsyncEvent.LipsyncType.Fave_lo, false, null),
                "TH" or "DH" => (LipsyncEvent.LipsyncType.Told_lo, false, null),
                "S" or "Z" => (LipsyncEvent.LipsyncType.Size_lo, false, null),
                "T" or "D" or "N" or "L" => (LipsyncEvent.LipsyncType.Told_lo, false, null),
                "SH" or "ZH" or "CH" or "JH" => (LipsyncEvent.LipsyncType.Told_lo, false, null),
                "R" => (LipsyncEvent.LipsyncType.Roar_lo, false, null),
                "W" => (LipsyncEvent.LipsyncType.Wet_lo, false, null),
                "Y" => (LipsyncEvent.LipsyncType.Eat_lo, false, null),
                
                _ => (LipsyncEvent.LipsyncType.Neutral_lo, false, null)
            };
        }
    }
}
