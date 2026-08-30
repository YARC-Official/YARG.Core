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
    /// <remarks>
    /// Only <c>_lo</c> visemes are emitted. Authored Milo data pairs most <c>_lo</c> keyframes
    /// with a matching <c>_hi</c> channel, but YARG avatars define no <c>_hi</c> expression
    /// clips and the runtime falls back to a single generic mouth key for unknown viseme names,
    /// so emitting <c>_hi</c> today only doubles the event count with no visual effect.
    /// </remarks>
    public static class LipsyncGenerator
    {
        private const double ATTACK_MAX_TIME = 0.20;    // Consonant/vowel attack ramp length
        private const double CODA_MAX_TIME = 0.15;      // Final-consonant ramp length
        private const double RELEASE_TIME = 0.20;       // Mouth close ramp length
        private const double RELEASE_THRESHOLD = 0.30;  // Minimum gap before releasing the mouth
        private const double STEP_TIME = 1.0 / 30;      // ~30fps interpolation steps (Milo-style keyframes)
        private const double MIN_SLOT_DURATION = 0.05;  // Minimum syllable slot duration
        private const double MAX_UNVOICED_SLOT = 2.0;   // Max slot length when no note end caps it

        // Section-level brow state: authored data keeps brow/emotional channels active for most of
        // the song (stacked, sustained from seconds to minutes), so one brow state is chosen per
        // section of phrases and sustained across it instead of firing per-phrase blips
        private const double BROW_SECTION_GAP = 2.0;  // Silent gap that starts a new brow section
        private const double BROW_FADE_TIME = 0.8;    // Brow ramp in/out length
        private const float EXPRESSION_CHANCE = 0.08f; // Chance a section uses a full-face expression

        // Co-articulation: the outgoing shape keeps a faint residual while the new one rises,
        // so transitions overlap like authored keyframes instead of snapping between poses
        private const float CO_ARTICULATION_RESIDUAL = 0.08f;
        private const double CO_ARTICULATION_FADE_FRACTION = 0.6; // Ramp fraction spent fading to the residual

        // Vowel peak scales with note length (longer/held notes open fuller, like authored lipsync).
        // The vowel rides a peak-shaped envelope: peak at the syllable, decaying to a low tail —
        // authored keyframes spend most of their time at low weights and rarely reach full open.
        private const float VOWEL_PEAK_BASE_WEIGHT = 0.20f;   // Peak weight floor for short syllables
        private const float VOWEL_PEAK_SCALE = 0.35f;         // Peak weight growth per second of note length
        private const float VOWEL_PEAK_CAP = 0.90f;           // Maximum vowel peak weight
        private const float VOWEL_TAIL_WEIGHT = 0.05f;        // Weight the vowel decays to by the slot end
        private const double VOWEL_PEAK_HOLD_FRACTION = 0.30; // Fraction of a short slot spent at/near peak
        private const double VOWEL_PEAK_HOLD_TIME = 0.30;     // Absolute cap on the peak hold (long slots)
        private const double VOWEL_DECAY_TIME = 0.60;         // Absolute decay time from peak to tail
        private const float CONSONANT_WEIGHT = 0.32f;         // Authored consonant means measure ~0.28

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
            var mouth = new MouthState();

            EmitBrowStates(events, lyrics.Phrases, random);

            for (int i = 0; i < lyrics.Phrases.Count; i++)
            {
                var phrase = lyrics.Phrases[i];
                if (phrase.Lyrics.Count == 0)
                    continue;

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
            var mouth = new MouthState();

            EmitBrowStates(events, part.StaticLyricPhrases, random);

            for (int i = 0; i < part.StaticLyricPhrases.Count; i++)
            {
                var phrase = part.StaticLyricPhrases[i];
                if (phrase.Lyrics.Count == 0)
                    continue;

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

        /// <summary>
        /// Sustained brow state machine: groups lyric phrases into sections (separated by silent
        /// gaps longer than <see cref="BROW_SECTION_GAP"/>) and emits one brow state per section,
        /// ramped in/out slowly and held across the section's phrases and internal gaps.
        /// </summary>
        private static void EmitBrowStates(List<LipsyncEvent> events, List<LyricsPhrase> phrases,
            Random random)
        {
            var spans = new List<(double Start, double End)>(phrases.Count);
            foreach (var phrase in phrases)
            {
                if (phrase.Lyrics.Count > 0)
                    spans.Add((phrase.Time, phrase.Time + phrase.TimeLength));
            }
            EmitBrowSections(events, spans, random);
        }

        private static void EmitBrowStates(List<LipsyncEvent> events, List<VocalsPhrase> phrases,
            Random random)
        {
            var spans = new List<(double Start, double End)>(phrases.Count);
            foreach (var phrase in phrases)
            {
                if (phrase.Lyrics.Count > 0)
                    spans.Add((phrase.Time, phrase.Time + phrase.TimeLength));
            }
            EmitBrowSections(events, spans, random);
        }

        private static void EmitBrowSections(List<LipsyncEvent> events,
            List<(double Start, double End)> spans, Random random)
        {
            // Precondition: spans are time-ordered (phrase lists from the chart are)
            int i = 0;
            while (i < spans.Count)
            {
                double start = spans[i].Start;
                double end = spans[i].End;
                int j = i + 1;
                while (j < spans.Count && spans[j].Start - end <= BROW_SECTION_GAP)
                {
                    end = Math.Max(end, spans[j].End);
                    j++;
                }

                EmitBrowSection(events, start, end, random);
                i = j;
            }
        }

        private static void EmitBrowSection(List<LipsyncEvent> events, double start, double end,
            Random random)
        {
            if (end - start < 0.1)
                return;

            var primary = PickBrowType(random, LipsyncEvent.LipsyncType.Neutral_lo);
            EmitBrowChannel(events, primary, 0.3f + (float) random.NextDouble() * 0.4f, start, end);

            // Authored data often stacks a faint second brow channel under the primary one
            if (random.NextDouble() < 0.4)
            {
                var secondary = PickBrowType(random, primary);
                EmitBrowChannel(events, secondary,
                    0.1f + (float) random.NextDouble() * 0.15f, start, end);
            }
        }

        private static void EmitBrowChannel(List<LipsyncEvent> events, LipsyncEvent.LipsyncType type,
            float intensity, double start, double end)
        {
            double fade = Math.Min(BROW_FADE_TIME, (end - start) * 0.25);

            // Ease the brow in and out instead of snapping between face poses
            EmitRamp(events, type, 0f, intensity, start, fade);
            EmitRamp(events, type, intensity, intensity, start + fade,
                Math.Max(0, end - start - fade * 2));
            EmitRamp(events, type, intensity, 0f, end - fade, fade);
        }

        private static readonly (LipsyncEvent.LipsyncType Type, float Weight)[] BrowPalette =
        {
            // Weights roughly follow authored segment counts across the reference charts
            (LipsyncEvent.LipsyncType.Brow_down, 5f),
            (LipsyncEvent.LipsyncType.Brow_pouty, 5f),
            (LipsyncEvent.LipsyncType.Brow_aggressive, 5f),
            (LipsyncEvent.LipsyncType.Brow_up, 2f),
            (LipsyncEvent.LipsyncType.Squint, 1.5f),
            (LipsyncEvent.LipsyncType.Brow_dramatic, 1f),
        };

        private static LipsyncEvent.LipsyncType PickBrowType(Random random,
            LipsyncEvent.LipsyncType exclude)
        {
            // Rare full-face expression instead of a brow, like authored data
            if (exclude == LipsyncEvent.LipsyncType.Neutral_lo
                && random.NextDouble() < EXPRESSION_CHANCE)
            {
                return LipsyncEvent.LipsyncType.exp_rocker_smile_intense_01;
            }

            float total = 0;
            foreach (var entry in BrowPalette)
            {
                if (entry.Type != exclude)
                    total += entry.Weight;
            }

            float roll = (float) (random.NextDouble() * total);
            foreach (var entry in BrowPalette)
            {
                if (entry.Type == exclude)
                    continue;

                roll -= entry.Weight;
                if (roll <= 0)
                    return entry.Type;
            }

            return LipsyncEvent.LipsyncType.Brow_down;
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
                else
                {
                    // No vocal note to time against (lyrics-track generation on charts without
                    // vocal notes): a phrase-final word would otherwise stretch its slot to the
                    // phrase end — seconds past the sung word — holding the mouth open through
                    // the silence. Cap it like an actual sung word length.
                    slotEnd = Math.Min(slotEnd, frag.Time + MAX_UNVOICED_SLOT);
                }

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
            float vowelPeak = hasVowel
                ? Math.Min(VOWEL_PEAK_CAP, VOWEL_PEAK_BASE_WEIGHT + (float) duration * VOWEL_PEAK_SCALE)
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
                    RampTo(events, ref mouth, syll.VowelMain, vowelPeak, t, seg, slot.Tick);
                    t += seg;
                }
            }

            // With a pre-roll the vowel already peaks at the slot start.
            double holdStart = Math.Max(t, slot.Start);
            double holdEnd = slot.End - coda;
            if (hasVowel)
            {
                // Vowel rides a peak-shaped envelope, decaying toward the tail weight by the slot
                // end like authored vowels. With a pre-roll the peak already sits at the lyric.
                if (syll.VowelEnd.HasValue)
                {
                    // Diphthong: hold the main vowel 60%, then glide to its end shape over 40%
                    double transitionStart = holdStart + (holdEnd - holdStart) * 0.6;
                    float glideWeight = EmitVowelEnvelope(events, ref mouth, vowelPeak,
                        holdStart, transitionStart, holdEnd, slot.Tick);
                    mouth.Weight = glideWeight;
                    RampTo(events, ref mouth, syll.VowelEnd.Value, glideWeight, transitionStart,
                        holdEnd - transitionStart, slot.Tick);
                }
                else
                {
                    mouth.Weight = EmitVowelEnvelope(events, ref mouth, vowelPeak,
                        holdStart, holdEnd, holdEnd, slot.Tick);
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
        /// Emits the vowel hold as a peak-shaped envelope sampled every ~30fps frame: peak weight
        /// at the start of the hold (the attack already peaks at the lyric), sustained briefly,
        /// then decaying to a low tail like authored Milo vowels. Weight decays relative to
        /// <paramref name="endTime"/>, so a diphthong glide that cuts the hold short leaves the
        /// envelope mid-decay. Returns the weight at <paramref name="stopTime"/>.
        /// </summary>
        private static float EmitVowelEnvelope(List<LipsyncEvent> events, ref MouthState mouth,
            float peakWeight, double startTime, double stopTime, double endTime, uint tick)
        {
            if (mouth.Weight <= 0.001f || stopTime <= startTime)
                return mouth.Weight;

            // The attack ramp's final key already sits at the hold start with the same shape;
            // skip a duplicate leading frame.
            bool skipFirst = Math.Abs(startTime - mouth.LastEmissionEnd) < 0.001;

            // The peak is sustained briefly and decays on an ABSOLUTE timescale (not proportional
            // to the slot): a long vocal note must settle near-closed like authored vowels instead
            // of holding a wide-open pose for seconds.
            double slotLen = endTime - startTime;
            double holdEnd = startTime + Math.Min(VOWEL_PEAK_HOLD_TIME, slotLen * VOWEL_PEAK_HOLD_FRACTION);
            double decayEnd = Math.Min(holdEnd + VOWEL_DECAY_TIME, endTime);

            float lastWeight = peakWeight;
            int steps = (int) Math.Ceiling((stopTime - startTime) / STEP_TIME);
            for (int i = 0; i < steps; i++)
            {
                if (i == 0 && skipFirst)
                    continue;

                double t = startTime + i * STEP_TIME;
                float weight;
                if (t <= holdEnd)
                {
                    weight = peakWeight;
                }
                else if (t < decayEnd)
                {
                    double raw = (t - holdEnd) / (decayEnd - holdEnd);
                    double ease = raw * raw * (3.0 - 2.0 * raw); // Smoothstep decay
                    weight = peakWeight + (VOWEL_TAIL_WEIGHT - peakWeight) * (float) ease;
                }
                else
                {
                    weight = VOWEL_TAIL_WEIGHT;
                }
                events.Add(new LipsyncEvent(mouth.Type, weight, t, tick));
                lastWeight = weight;
            }
            mouth.LastEmissionEnd = stopTime;
            return lastWeight;
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
                    {
                        // Co-articulation: fade the outgoing shape to a small residual first, then
                        // out, leaving both shapes faintly active mid-transition like authored data
                        float residual = Math.Min(CO_ARTICULATION_RESIDUAL, fromWeight);
                        float outWeight;
                        if (t < CO_ARTICULATION_FADE_FRACTION)
                        {
                            float fade = t / (float) CO_ARTICULATION_FADE_FRACTION;
                            outWeight = residual + (fromWeight - residual) * (1 - fade);
                        }
                        else
                        {
                            float fade = (float) ((t - CO_ARTICULATION_FADE_FRACTION)
                                / (1.0 - CO_ARTICULATION_FADE_FRACTION));
                            outWeight = residual * (1 - fade);
                        }

                        if (outWeight > 0.001f)
                            events.Add(new LipsyncEvent(fromType, outWeight, time, tick));
                    }
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
