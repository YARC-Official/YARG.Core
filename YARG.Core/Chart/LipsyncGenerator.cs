using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Generates lipsync events from lyrics for charts that don't provide their own lipsync data.
    /// </summary>
    /// <remarks>
    /// Words split across multiple lyric syllables are re-joined before phoneme lookup, so the
    /// full word is analyzed and its phonemes are distributed across the syllables. Timings use
    /// the vocal notes' actual lengths when available, and mouth shapes are ramped in/out
    /// smoothly instead of snapping.
    /// </remarks>
    public static class LipsyncGenerator
    {
        private const double ATTACK_MAX_TIME = 0.20;    // Consonant/vowel attack ramp length
        private const double CODA_MAX_TIME = 0.15;      // Final-consonant ramp length
        private const double RELEASE_TIME = 0.20;       // Mouth close ramp length
        private const double RELEASE_THRESHOLD = 0.30;  // Minimum gap before releasing the mouth
        private const double STEP_TIME = 1.0 / 30;      // ~30fps interpolation steps (Milo-style keyframes)
        private const double MIN_SLOT_DURATION = 0.05;  // Minimum syllable slot duration

        // Vowel openness scales with note length (longer/held notes open fuller, like authored lipsync)
        private const float VOWEL_BASE_WEIGHT = 0.55f;
        private const float VOWEL_WEIGHT_SCALE = 1.0f;
        private const float VOWEL_WEIGHT_CAP = 0.95f;
        private const float CONSONANT_WEIGHT = 0.45f;

        private static IReadOnlyDictionary<string, string[]>? _cmuDict;

        public static void Initialize(string dictionaryText)
        {
            CMUDict.Initialize(dictionaryText);
            _cmuDict = CMUDict.GetDictionary();
        }

        public static List<LipsyncEvent> GenerateFromLyrics(LyricsTrack lyrics)
        {
            var noteEndsByTick = new Dictionary<uint, double>();

            var events = new List<LipsyncEvent>();
            var random = new Random();
            var nextBlinkTime = 2.0 + random.NextDouble() * 3.0; // First blink between 2-5s
            var nextExpressionTime = 4.0 + random.NextDouble() * 4.0; // First expression between 4-8s
            var mouth = new MouthState();

            for (int i = 0; i < lyrics.Phrases.Count; i++)
            {
                var phrase = lyrics.Phrases[i];
                if (phrase.Lyrics.Count == 0)
                    continue;

                AddPhraseExpression(events, phrase.Time, phrase.TimeLength, random, ref nextExpressionTime);
                ProcessPhrase(events, phrase.Lyrics, phrase.Time + phrase.TimeLength, noteEndsByTick, random,
                    ref nextBlinkTime, ref mouth);

                // Close the mouth during silent gaps between phrases
                double? nextPhraseStart = null;
                for (int p = i + 1; p < lyrics.Phrases.Count && nextPhraseStart == null; p++)
                {
                    if (lyrics.Phrases[p].Lyrics.Count > 0)
                        nextPhraseStart = lyrics.Phrases[p].Lyrics[0].Time;
                }
                ReleaseAfterPhrase(events, ref mouth, phrase.Time + phrase.TimeLength, nextPhraseStart);
            }

            return events.OrderBy(e => e.Time).ToList();
        }

        public static List<LipsyncEvent> GenerateFromVocalsPart(VocalsPart part,
            IEnumerable<VocalsPart>? noteSources = null)
        {
            // Merge note ends across all harmony parts: a word is vocalized as long as any part
            // sustains it, even if this part's own charting cut the note short.
            var noteEndsByTick = BuildNoteEndMap(noteSources ?? new[] { part });

            var events = new List<LipsyncEvent>();
            var random = new Random();
            var nextBlinkTime = 2.0 + random.NextDouble() * 3.0; // First blink between 2-5s
            var nextExpressionTime = 4.0 + random.NextDouble() * 4.0; // First expression between 4-8s
            var mouth = new MouthState();

            for (int i = 0; i < part.StaticLyricPhrases.Count; i++)
            {
                var phrase = part.StaticLyricPhrases[i];
                if (phrase.Lyrics.Count == 0)
                    continue;

                AddPhraseExpression(events, phrase.Time, phrase.TimeLength, random, ref nextExpressionTime);
                ProcessPhrase(events, phrase.Lyrics, phrase.Time + phrase.TimeLength, noteEndsByTick, random,
                    ref nextBlinkTime, ref mouth);

                // Close the mouth during silent gaps between phrases
                double? nextPhraseStart = null;
                for (int p = i + 1; p < part.StaticLyricPhrases.Count && nextPhraseStart == null; p++)
                {
                    if (part.StaticLyricPhrases[p].Lyrics.Count > 0)
                        nextPhraseStart = part.StaticLyricPhrases[p].Lyrics[0].Time;
                }
                ReleaseAfterPhrase(events, ref mouth, phrase.Time + phrase.TimeLength, nextPhraseStart);
            }

            return events.OrderBy(e => e.Time).ToList();
        }

        /// <summary>
        /// Maps lyric ticks to the end times of their associated vocal notes, merged across all
        /// harmony parts. A word is vocalized as long as any part sustains it, so the longest
        /// note wins.
        /// </summary>
        private static Dictionary<uint, double> BuildNoteEndMap(IEnumerable<VocalsPart> parts)
        {
            var noteEnds = new Dictionary<uint, double>();
            foreach (var part in parts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    var lyrics = phrase.Lyrics;
                    var notes = phrase.PhraseParentNote.ChildNotes;
                    for (int i = 0; i < lyrics.Count && i < notes.Count; i++)
                    {
                        var tick = lyrics[i].Tick;
                        if (!noteEnds.TryGetValue(tick, out var noteEnd) || notes[i].TimeEnd > noteEnd)
                            noteEnds[tick] = notes[i].TimeEnd;
                    }
                }
            }
            return noteEnds;
        }

        private static void AddPhraseExpression(List<LipsyncEvent> events, double phraseStart,
            double phraseDuration, Random random, ref double nextExpressionTime)
        {
            if (phraseStart <= nextExpressionTime || random.NextDouble() <= 0.5)
                return;

            var expressions = new[]
            {
                LipsyncEvent.LipsyncType.Brow_up,
                LipsyncEvent.LipsyncType.Brow_down,
                LipsyncEvent.LipsyncType.exp_rocker_smile_mellow_01,
                LipsyncEvent.LipsyncType.exp_rocker_teethgrit_happy_01,
                LipsyncEvent.LipsyncType.exp_dramatic_happy_eyesopen_01,
            };

            var expression = expressions[random.Next(expressions.Length)];
            var intensity = 0.3f + (float) random.NextDouble() * 0.4f; // 0.3 to 0.7
            var expressionDuration = Math.Min(phraseDuration * 0.6, 1.5); // Max 1.5s or 60% of phrase

            // Ease the expression in and out instead of snapping between face poses
            const double expressionFade = 0.3;
            EmitRamp(events, expression, 0f, intensity, phraseStart, expressionFade);
            EmitRamp(events, expression, intensity, intensity, phraseStart + expressionFade,
                Math.Max(0, expressionDuration - expressionFade * 2));
            EmitRamp(events, expression, intensity, 0f, phraseStart + expressionDuration - expressionFade,
                expressionFade);

            nextExpressionTime = phraseStart + phraseDuration + 3.0 + random.NextDouble() * 5.0;
        }

        /// <summary>
        /// Emits a dense per-frame ramp for a non-viseme expression, like authored Milo keyframes.
        /// </summary>
        private static void EmitRamp(List<LipsyncEvent> events, LipsyncEvent.LipsyncType type,
            float fromWeight, float toWeight, double startTime, double duration)
        {
            if (duration <= 0)
                return;

            int steps = Math.Max(1, (int) Math.Ceiling(duration / STEP_TIME));
            double stepDur = duration / steps;
            for (int i = 0; i <= steps; i++)
            {
                float rawT = (float) i / steps;
                float t = rawT * rawT * (3 - 2 * rawT); // Smoothstep easing
                events.Add(new LipsyncEvent(type, fromWeight + (toWeight - fromWeight) * t,
                    startTime + i * stepDur, 0));
            }
        }

        private static void ProcessPhrase(List<LipsyncEvent> events, List<LyricEvent> lyrics,
            double phraseEnd, Dictionary<uint, double> noteEndsByTick, Random random,
            ref double nextBlinkTime, ref MouthState mouth)
        {
            int count = lyrics.Count;
            int i = 0;
            while (i < count)
            {
                var text = LyricSymbols.StripForVocals(lyrics[i].Text);
                if (string.IsNullOrWhiteSpace(text))
                {
                    i++;
                    continue;
                }

                // Add blinks if enough time has passed
                while (nextBlinkTime < lyrics[i].Time)
                {
                    // Fast but smooth blink (~0.15s total, like authored animation)
                    EmitRamp(events, LipsyncEvent.LipsyncType.Blink, 0f, 1f, nextBlinkTime, 0.05);
                    EmitRamp(events, LipsyncEvent.LipsyncType.Blink, 1f, 0f, nextBlinkTime + 0.05, 0.10);
                    nextBlinkTime += 2.0 + random.NextDouble() * 4.0; // Next blink in 2-6s
                }

                // Collect the full word: joined syllables plus trailing slide-gap extensions
                var wordLyrics = new List<LyricEvent>();
                var wordTexts = new List<string>();
                bool lastRealJoins = true;
                int j = i;
                while (j < count && lastRealJoins)
                {
                    var wordText = LyricSymbols.StripForVocals(lyrics[j].Text);
                    wordLyrics.Add(lyrics[j]);
                    wordTexts.Add(wordText);

                    if (!string.IsNullOrWhiteSpace(wordText))
                        lastRealJoins = lyrics[j].JoinOrHyphenateWithNext;
                    j++;
                }

                GenerateWord(events, wordLyrics, wordTexts, j, lyrics, phraseEnd, noteEndsByTick, ref mouth);
                i = j;
            }
        }

        private static void GenerateWord(List<LipsyncEvent> events, List<LyricEvent> wordLyrics,
            List<string> wordTexts, int flatIndexAfterWord, List<LyricEvent> allLyrics,
            double phraseEnd, Dictionary<uint, double> noteEndsByTick, ref MouthState mouth)
        {
            var wordText = string.Concat(wordTexts);
            var syllables = GetSyllablesForWord(wordText);
            if (syllables.Count == 0)
                return;

            YargLogger.LogFormatTrace("Lipsync word '{0}' at {1:F3}s -> {2} syllable(s)",
                wordText, wordLyrics[0].Time, syllables.Count);

            // Slide-gap fragments immediately after the word extend its audible length: they mark
            // the sung continuation (pitch slides) even when no vocal notes exist for them.
            double extensionEnd = -1;
            int k = flatIndexAfterWord;
            while (k < allLyrics.Count && string.IsNullOrWhiteSpace(LyricSymbols.StripForVocals(allLyrics[k].Text)))
            {
                // Use the slide fragment's own time as the minimum sung extent, or its note end
                // when the vocals part does have a note for it.
                double candidate = allLyrics[k].Time;
                if (noteEndsByTick.TryGetValue(allLyrics[k].Tick, out var gapEnd) && gapEnd > candidate)
                    candidate = gapEnd;
                if (candidate > extensionEnd)
                    extensionEnd = candidate;
                k++;
            }

            double nextWordStart = k < allLyrics.Count ? allLyrics[k].Time : phraseEnd;

            // Real (non-empty) syllable fragments
            var realIndices = new List<int>();
            for (int w = 0; w < wordLyrics.Count; w++)
            {
                if (!string.IsNullOrWhiteSpace(wordTexts[w]))
                    realIndices.Add(w);
            }
            if (realIndices.Count == 0)
                return;

            int fragCount = realIndices.Count;
            int syllCount = syllables.Count;

            // Computes the audible end of the syllable slot for real fragment index fi
            double SlotEnd(int fi)
            {
                var frag = wordLyrics[realIndices[fi]];
                double nextStart = fi + 1 < fragCount
                    ? wordLyrics[realIndices[fi + 1]].Time
                    : nextWordStart;
                double slotEnd = nextStart;

                // Cap the slot at the vocal note's end: the closing consonants belong to the
                // sung portion of the word, not to the silence after it. For legato lines where
                // the note runs into the next lyric, the next lyric wins. Slide-gap fragments
                // extend the audible length past the note end when the word keeps being sung.
                double audibleEnd = -1;
                if (noteEndsByTick.TryGetValue(frag.Tick, out var noteEnd) && noteEnd > frag.Time)
                    audibleEnd = noteEnd;
                if (fi == fragCount - 1)
                    audibleEnd = Math.Max(audibleEnd, extensionEnd);

                if (audibleEnd > 0)
                    slotEnd = Math.Min(slotEnd, Math.Max(audibleEnd, frag.Time + MIN_SLOT_DURATION));

                slotEnd = Math.Max(slotEnd, frag.Time + MIN_SLOT_DURATION);
                return Math.Min(slotEnd, phraseEnd);
            }

            var slots = new List<Slot>();
            if (syllCount >= fragCount)
            {
                // Distribute syllables across the fragments
                for (int fi = 0; fi < fragCount; fi++)
                {
                    int firstSyll = (int) Math.Round(fi * (double) syllCount / fragCount);
                    int lastSyll = (int) Math.Round((fi + 1) * (double) syllCount / fragCount);
                    int owned = Math.Max(1, lastSyll - firstSyll);

                    double start = wordLyrics[realIndices[fi]].Time;
                    double end = SlotEnd(fi);
                    double step = (end - start) / owned;

                    for (int s = 0; s < owned && firstSyll + s < syllCount; s++)
                    {
                        slots.Add(new Slot
                        {
                            Syllable = syllables[firstSyll + s],
                            Start = start + s * step,
                            End = start + (s + 1) * step,
                            Tick = wordLyrics[realIndices[fi]].Tick,
                        });
                    }
                }
            }
            else
            {
                // More fragments than syllables: reuse the nearest syllable
                for (int fi = 0; fi < fragCount; fi++)
                {
                    int syllIndex = syllCount == 1
                        ? 0
                        : Math.Clamp((int) Math.Round(fi * (double) (syllCount - 1) / (fragCount - 1)), 0, syllCount - 1);
                    slots.Add(new Slot
                    {
                        Syllable = syllables[syllIndex],
                        Start = wordLyrics[realIndices[fi]].Time,
                        End = SlotEnd(fi),
                        Tick = wordLyrics[realIndices[fi]].Tick,
                    });
                }
            }

            foreach (var slot in slots)
                EmitSyllableSlot(events, ref mouth, in slot);

            // Release into the gap after the word
            double lastEnd = slots[^1].End;
            if (nextWordStart - lastEnd > RELEASE_THRESHOLD)
            {
                double releaseDur = Math.Min(RELEASE_TIME, nextWordStart - lastEnd);
                ReleaseMouth(events, ref mouth, lastEnd, releaseDur, slots[^1].Tick);
            }
        }

        private static void ReleaseAfterPhrase(List<LipsyncEvent> events, ref MouthState mouth,
            double phraseEnd, double? nextPhraseStart)
        {
            if (mouth.Weight <= 0.001f)
                return;

            // If the next phrase is unknown/far away, just release
            double gapEnd = nextPhraseStart ?? (phraseEnd + RELEASE_TIME);
            if (gapEnd - phraseEnd < 0.005)
                return;

            ReleaseMouth(events, ref mouth, phraseEnd, Math.Min(RELEASE_TIME, gapEnd - phraseEnd), 0);
        }

        private static void EmitSyllableSlot(List<LipsyncEvent> events, ref MouthState mouth, in Slot slot)
        {
            double duration = slot.End - slot.Start;
            if (duration <= 0.01)
                return;

            var syll = slot.Syllable;
            bool hasVowel = syll.VowelMain != LipsyncEvent.LipsyncType.Neutral_lo || syll.VowelEnd.HasValue;

            // Vowel openness scales with the note's length; consonants sit at a partial weight
            float vowelWeight = hasVowel
                ? Math.Min(VOWEL_WEIGHT_CAP, VOWEL_BASE_WEIGHT + (float) duration * VOWEL_WEIGHT_SCALE)
                : 0f;

            int attackSegments = syll.Initial.Count + (hasVowel ? 1 : 0);

            // Anticipation: place the attack BEFORE the syllable (in the gap after the previous
            // syllable) so the mouth peaks at the lyric, like authored Milo lipsync. When singing
            // is continuous (no gap), compress the attack into the first couple of frames instead
            // of building up late.
            double desiredAttack = attackSegments > 0 ? Math.Min(ATTACK_MAX_TIME, duration * 0.4) : 0;
            double gap = slot.Start - mouth.LastEmissionEnd;
            double attack;
            double attackStart;
            if (attackSegments > 0 && gap > 0.005)
            {
                attack = Math.Min(desiredAttack, gap);
                attackStart = slot.Start - attack;
            }
            else if (attackSegments > 0)
            {
                attack = Math.Min(desiredAttack, 0.07);
                attackStart = slot.Start;
            }
            else
            {
                attack = 0;
                attackStart = slot.Start;
            }
            double coda = syll.Final.Count > 0 ? Math.Min(CODA_MAX_TIME, duration * 0.25) : 0;

            // Never let attack + coda consume the whole slot
            if (attack + coda > duration * 0.9)
            {
                double scale = duration * 0.9 / (attack + coda);
                attack *= scale;
                coda *= scale;
            }

            // Attack: ramp through initial consonants, then into the vowel, peaking at the slot start
            double t = attackStart;
            if (attackSegments > 0)
            {
                double seg = attack / attackSegments;
                foreach (var consonant in syll.Initial)
                {
                    RampTo(events, ref mouth, consonant, CONSONANT_WEIGHT, t, seg, slot.Tick);
                    t += seg;
                }

                if (hasVowel)
                {
                    RampTo(events, ref mouth, syll.VowelMain, vowelWeight, t, seg, slot.Tick);
                    t += seg;
                }
            }

            // Hold the vowel with dense per-frame keys so weights glide like Milo keyframes.
            // With a pre-roll the vowel already peaks at the slot start.
            double holdStart = Math.Max(t, slot.Start);
            double holdEnd = slot.End - coda;
            // Hold the vowel with dense per-frame keys
            if (hasVowel)
            {
                if (syll.VowelEnd.HasValue)
                {
                    // Diphthong: hold the main vowel 60%, then glide to its end shape over 40%
                    double transitionStart = holdStart + (holdEnd - holdStart) * 0.6;
                    EmitHold(events, ref mouth, holdStart, transitionStart, slot.Tick);
                    RampTo(events, ref mouth, syll.VowelEnd.Value, vowelWeight, transitionStart,
                        holdEnd - transitionStart, slot.Tick);
                }
                else
                {
                    EmitHold(events, ref mouth, holdStart, holdEnd, slot.Tick);
                }
            }
            else
            {
                // Consonant-only syllable: hold the current shape
                EmitHold(events, ref mouth, holdStart, holdEnd, slot.Tick);
            }

            // Coda: ramp through final consonants up to the slot end
            if (coda > 0)
            {
                double codaSeg = coda / syll.Final.Count;
                double ct = slot.End - coda;
                foreach (var consonant in syll.Final)
                {
                    RampTo(events, ref mouth, consonant, CONSONANT_WEIGHT, ct, codaSeg, slot.Tick);
                    ct += codaSeg;
                }
            }
        }

        /// <summary>
        /// Re-emits the current mouth state every ~30fps frame during a hold, like authored Milo keyframes.
        /// </summary>
        private static void EmitHold(List<LipsyncEvent> events, ref MouthState mouth,
            double startTime, double endTime, uint tick)
        {
            if (mouth.Weight <= 0.001f)
                return;

            for (double t = startTime; t < endTime; t += STEP_TIME)
                events.Add(new LipsyncEvent(mouth.Type, mouth.Weight, t, tick));
            mouth.LastEmissionEnd = endTime;
        }

        /// <summary>
        /// Ramps the mouth from its current shape to a new shape, emitting interpolation steps.
        /// </summary>
        private static void RampTo(List<LipsyncEvent> events, ref MouthState mouth,
            LipsyncEvent.LipsyncType toType, float toWeight, double startTime, double duration, uint tick)
        {
            var fromType = mouth.Type;
            var fromWeight = mouth.Weight;

            if (fromType == toType && Math.Abs(fromWeight - toWeight) < 0.001f)
                return;

            if (duration <= 0.005)
            {
                if (fromWeight > 0.001f && fromType != toType)
                    events.Add(new LipsyncEvent(fromType, 0f, startTime, tick));
                events.Add(new LipsyncEvent(toType, toWeight, startTime, tick));
            }
            else
            {
                int steps = Math.Max(1, (int) Math.Ceiling(duration / STEP_TIME));
                double stepDur = duration / steps;
                bool emitFrom = fromWeight > 0.001f && fromType != toType;

                for (int i = 0; i <= steps; i++)
                {
                    float rawT = (float) i / steps;
                    // Smoothstep easing for S-curved motion, like authored lipsync ramps
                    float t = rawT * rawT * (3 - 2 * rawT);
                    double time = startTime + i * stepDur;

                    if (emitFrom)
                        events.Add(new LipsyncEvent(fromType, fromWeight * (1 - t), time, tick));
                    events.Add(new LipsyncEvent(toType, fromWeight + (toWeight - fromWeight) * t, time, tick));
                }
            }

            mouth.Type = toType;
            mouth.Weight = toWeight;
            mouth.LastEmissionEnd = startTime + duration;
        }

        private static void ReleaseMouth(List<LipsyncEvent> events, ref MouthState mouth,
            double startTime, double duration, uint tick)
        {
            if (mouth.Weight > 0.001f)
                RampTo(events, ref mouth, mouth.Type, 0f, startTime, duration, tick);
        }

        private struct MouthState
        {
            public LipsyncEvent.LipsyncType Type;
            public float Weight;
            public double LastEmissionEnd;  // Time of the last emitted mouth event
        }

        private struct Slot
        {
            public Syllable Syllable;
            public double Start;
            public double End;
            public uint Tick;
        }

        private struct Syllable
        {
            public List<LipsyncEvent.LipsyncType> Initial;
            public LipsyncEvent.LipsyncType VowelMain;
            public LipsyncEvent.LipsyncType? VowelEnd;
            public List<LipsyncEvent.LipsyncType> Final;
        }

        private static Syllable NewSyllable() => new()
        {
            Initial = new List<LipsyncEvent.LipsyncType>(),
            VowelMain = LipsyncEvent.LipsyncType.Neutral_lo,
            VowelEnd = null,
            Final = new List<LipsyncEvent.LipsyncType>(),
        };

        /// <summary>
        /// Splits a full word into syllables and converts them to visemes.
        /// </summary>
        private static List<Syllable> GetSyllablesForWord(string text)
        {
            var clean = CleanLyricText(text);
            if (clean.Length == 0)
                return new List<Syllable>();

            if (TryGetPhonemes(clean, out var phonemes) && phonemes.Length > 0)
            {
                var syllables = SplitWordPhonemes(phonemes);
                if (syllables.Count > 0)
                    return syllables;
            }

            // Fallback to simple mapping
            return new List<Syllable> { SimpleSyllable(clean) };
        }

        private static string CleanLyricText(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case LyricSymbols.LYRIC_JOIN_SYMBOL:
                    case LyricSymbols.LYRIC_JOIN_HYPHEN_SYMBOL:
                    case LyricSymbols.PITCH_SLIDE_SYMBOL:
                    case LyricSymbols.NONPITCHED_SYMBOL:
                    case LyricSymbols.NONPITCHED_LENIENT_SYMBOL:
                    case LyricSymbols.NONPITCHED_UNKNOWN_SYMBOL:
                    case LyricSymbols.HARMONY_HIDE_SYMBOL:
                    case LyricSymbols.JOINED_SYLLABLE_SYMBOL:
                    case LyricSymbols.SPACE_ESCAPE_SYMBOL:
                    case ' ':
                    case '!':
                        break;
                    default:
                        sb.Append(char.ToLowerInvariant(c));
                        break;
                }
            }
            return sb.ToString();
        }

        private static List<Syllable> SplitWordPhonemes(string[] phonemes)
        {
            var syllables = new List<Syllable>();
            var current = NewSyllable();
            bool hasVowel = false;

            foreach (var phoneme in phonemes)
            {
                var (viseme, isDiphthong, diphthongEnd) = PhonemeToViseme(phoneme);
                if (viseme == LipsyncEvent.LipsyncType.Neutral_lo)
                    continue;

                if (IsVowelPhoneme(phoneme))
                {
                    if (hasVowel)
                    {
                        // Consonants between vowels begin the next syllable (maximum onset)
                        var next = NewSyllable();
                        next.Initial.AddRange(current.Final);
                        current.Final.Clear();
                        syllables.Add(current);
                        current = next;
                    }

                    current.VowelMain = viseme;
                    if (isDiphthong)
                        current.VowelEnd = diphthongEnd;
                    hasVowel = true;
                }
                else
                {
                    if (!hasVowel)
                        current.Initial.Add(viseme);
                    else
                        current.Final.Add(viseme);
                }
            }

            if (hasVowel)
            {
                syllables.Add(current);
            }
            else if (syllables.Count > 0)
            {
                // Trailing vowel-less cluster: fold into the last syllable
                syllables[^1].Final.AddRange(current.Initial);
            }
            else if (current.Initial.Count > 0)
            {
                // Vowel-less word: emit the consonants as a single consonant-only syllable
                var syll = NewSyllable();
                syll.Final.AddRange(current.Initial);
                syllables.Add(syll);
            }

            return syllables;
        }

        private static Syllable SimpleSyllable(string text)
        {
            var syllable = NewSyllable();

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
            phonemes = null!;
            if (_cmuDict == null) return false;
            var key = word.ToUpperInvariant();
            return _cmuDict.TryGetValue(key, out phonemes);
        }

        private static (LipsyncEvent.LipsyncType viseme, bool isDiphthong, LipsyncEvent.LipsyncType? diphthongEnd)
            PhonemeToViseme(string phoneme)
        {
            return phoneme switch
            {
                // Vowels
                // Mapping verified against handmade Milo lipsync data from 12 Rock Band charts
                // (per-phoneme lift analysis over ~3.2k CMU-resolved lyric-word windows)
                "AA" => (LipsyncEvent.LipsyncType.Ox_lo, false, null),
                "AE" => (LipsyncEvent.LipsyncType.Cage_lo, false, null),
                "AH" => (LipsyncEvent.LipsyncType.If_lo, false, null),
                "AO" => (LipsyncEvent.LipsyncType.Ox_lo, false, null),
                "EH" => (LipsyncEvent.LipsyncType.If_lo, false, null),
                "ER" => (LipsyncEvent.LipsyncType.Earth_lo, false, null),
                "IH" => (LipsyncEvent.LipsyncType.If_lo, false, null),
                "IY" => (LipsyncEvent.LipsyncType.Eat_lo, false, null),
                "UH" => (LipsyncEvent.LipsyncType.Though_lo, false, null),
                "UW" => (LipsyncEvent.LipsyncType.Wet_lo, false, null),

                // Diphthongs
                "AY" => (LipsyncEvent.LipsyncType.Eat_lo, true, LipsyncEvent.LipsyncType.If_lo),
                "EY" => (LipsyncEvent.LipsyncType.Ox_lo, true, LipsyncEvent.LipsyncType.If_lo),
                "OW" => (LipsyncEvent.LipsyncType.Ox_lo, false, null),
                "AW" => (LipsyncEvent.LipsyncType.Ox_lo, true, LipsyncEvent.LipsyncType.Wet_lo),
                "OY" => (LipsyncEvent.LipsyncType.Ox_lo, true, LipsyncEvent.LipsyncType.Eat_lo),

                // Consonants
                "B" or "P" or "M" => (LipsyncEvent.LipsyncType.Bump_lo, false, null),
                "F" or "V" => (LipsyncEvent.LipsyncType.Fave_lo, false, null),
                "TH" or "DH" => (LipsyncEvent.LipsyncType.Though_lo, false, null),
                "S" or "Z" => (LipsyncEvent.LipsyncType.Size_lo, false, null),
                "T" or "D" => (LipsyncEvent.LipsyncType.Told_lo, false, null),
                "N" => (LipsyncEvent.LipsyncType.New_lo, false, null),
                "L" => (LipsyncEvent.LipsyncType.Told_lo, false, null),
                "NG" => (LipsyncEvent.LipsyncType.New_lo, false, null),
                "K" or "G" => (LipsyncEvent.LipsyncType.Cage_lo, false, null),
                "SH" or "ZH" or "CH" or "JH" => (LipsyncEvent.LipsyncType.Church_lo, false, null),
                "R" => (LipsyncEvent.LipsyncType.Roar_lo, false, null),
                "W" => (LipsyncEvent.LipsyncType.Wet_lo, false, null),
                "Y" => (LipsyncEvent.LipsyncType.Eat_lo, false, null),

                _ => (LipsyncEvent.LipsyncType.Neutral_lo, false, null)
            };
        }
    }
}
