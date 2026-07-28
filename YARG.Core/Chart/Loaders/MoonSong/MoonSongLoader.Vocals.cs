using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Core.Utility;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public VocalsTrack LoadVocalsTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.Vocals => LoadSoloVocals(instrument),
                Instrument.Harmony => LoadHarmonyVocals(instrument),
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!", nameof(instrument))
            };
        }

        private VocalsTrack LoadSoloVocals(Instrument instrument)
        {
            var parts = new List<VocalsPart>()
            {
                LoadVocalsPart(MoonSong.MoonInstrument.Vocals),
            };

            var ranges = GetRangeShifts(parts, MoonSong.MoonInstrument.Vocals);
            var anims = GetAnimationTrack(instrument);
            return new VocalsTrack(instrument, parts, ranges, anims);
        }

        private VocalsTrack LoadHarmonyVocals(Instrument instrument)
        {
            var parts = new List<VocalsPart>()
            {
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony1),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony2),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony3),
            };

            var ranges = GetRangeShifts(parts, MoonSong.MoonInstrument.Harmony1);
            var anims = GetAnimationTrack(instrument);
            return new(instrument, parts, ranges, anims);
        }

        private VocalsPart LoadVocalsPart(MoonSong.MoonInstrument moonInstrument)
        {
            int harmonyPart = moonInstrument switch
            {
                MoonSong.MoonInstrument.Vocals or
                MoonSong.MoonInstrument.Harmony1 => 0,
                MoonSong.MoonInstrument.Harmony2 => 1,
                MoonSong.MoonInstrument.Harmony3 => 2,
                _ => throw new ArgumentException($"MoonInstrument {moonInstrument} is not a vocals instrument!", nameof(moonInstrument))
            };
            var moonChart = _moonSong.GetChart(moonInstrument, MoonSong.Difficulty.Expert);

            var isHarmony = moonInstrument != MoonSong.MoonInstrument.Vocals;
            var isBacking = harmonyPart is 1 or 2;
            var notePhrases = GetVocalsPhrases(moonChart, harmonyPart, false);

            // For solo vocals and HARM1, the static lyric phrases are always the same as the scoring phrases. HARM2 and HARM3 derive
            // their static lyric phrases from a different phrase type, so we have to run through again, looking for that type instead
            var staticLyricPhrases = GetVocalsPhrases(moonChart, harmonyPart, true);
            var otherPhrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            List<VocalsPhrase> mergedPhrases = new();
            TrimOrphanPhrases(notePhrases, otherPhrases);
            var staticShiftPhrases = moonChart.specialPhrases.Where(p => p.type == MoonPhrase.Type.Vocals_LyricShift).ToList();
            if (harmonyPart is 1)
            {
                var harm3Phrases =
                    GetVocalsPhrases(_moonSong.GetChart(MoonSong.MoonInstrument.Harmony3, MoonSong.Difficulty.Expert),
                        2, true);
                mergedPhrases = MergePhrases(staticLyricPhrases, harm3Phrases);
                SplitStaticLyricPhrases(ref mergedPhrases, staticShiftPhrases);
            }


            SplitStaticLyricPhrases(ref staticLyricPhrases, staticShiftPhrases);

            return new(isHarmony, notePhrases, staticLyricPhrases, mergedPhrases, otherPhrases, textEvents);
        }


        private List<VocalsPhrase> GetVocalsPhrases(MoonChart moonChart, int harmonyPart, bool staticLyricPhrases)
        {
            var phrases = new List<VocalsPhrase>();

            // Depending on the values of staticLyricPhrases and harmonyPart, we're either looking at regular phrases or
            // harmony lyric phrases. These two track which one we care about, and which one we don't
            MoonPhrase.Type lyricPhraseType;
            MoonPhrase.Type otherLyricPhraseType;

            if (harmonyPart is 0)
            {
                // For solo vocals and HARM1, we never care about harmony lyric phrases. LoadVocalsPart never calls with harmonyPart=0
                // and staticLyricPhrases=true anyway; it just reuses the result of the first call with staticLyricPhrases=false
                (lyricPhraseType, otherLyricPhraseType) = (MoonPhrase.Type.Vocals_ScoringPhrase, MoonPhrase.Type.Vocals_StaticLyricPhrase);
            } else
            {
                // For HARM2 and 3, it depends on the value of staticLyricPhrases. For each HARM2 and 3, LoadVocalsPart calls this method once
                // with staticLyricPhrases=false, to get the scoring phrases, and again with staticLyricPhrases=true, to get the static lyric
                // phrases.
                (lyricPhraseType, otherLyricPhraseType) = staticLyricPhrases ?
                    (MoonPhrase.Type.Vocals_StaticLyricPhrase, MoonPhrase.Type.Vocals_ScoringPhrase) :
                    (MoonPhrase.Type.Vocals_ScoringPhrase, MoonPhrase.Type.Vocals_StaticLyricPhrase);
            }


            // Prefill with the valid phrases
            var phraseTracker = new Dictionary<MoonPhrase.Type, MoonPhrase?>()
            {
                { MoonPhrase.Type.Starpower , null },
                { MoonPhrase.Type.Versus_Player1 , null },
                { MoonPhrase.Type.Versus_Player2 , null },
                { MoonPhrase.Type.Vocals_PercussionPhrase , null },
                { otherLyricPhraseType, null },
            };

            int moonNoteIndex = 0;
            int moonTextIndex = 0;

            VocalNote? carriedNote = null;
            VocalNote? previousParentLyric = null;
            for (int moonPhraseIndex = 0; moonPhraseIndex < moonChart.specialPhrases.Count;)
            {
                var moonPhrase = moonChart.specialPhrases[moonPhraseIndex++];
                if (moonPhrase.type != lyricPhraseType)
                {
                    phraseTracker[moonPhrase.type] = moonPhrase;
                    continue;
                }

                // Ensure any other phrases on the same tick get tracked
                while (moonPhraseIndex < moonChart.specialPhrases.Count)
                {
                    var moonPhrase2 = moonChart.specialPhrases[moonPhraseIndex];
                    if (moonPhrase2.tick > moonPhrase.tick)
                        break;

                    phraseTracker[moonPhrase2.type] = moonPhrase2;
                    moonPhraseIndex++;
                }

                if (carriedNote != null && carriedNote.Tick + carriedNote.TotalTickLength < moonPhrase.tick)
                {
                    carriedNote = null;
                }

                // Go through each note and lyric in the phrase
                var notes = new List<VocalNote>();
                var lyrics = new List<LyricEvent>();
                VocalNote? previousNote = null;
                uint endOfPhrase = moonPhrase.tick + moonPhrase.length;
                while (moonNoteIndex < moonChart.notes.Count)
                {
                    var moonNote = moonChart.notes[moonNoteIndex];
                    if (moonNote.tick >= endOfPhrase)
                        break;
                    moonNoteIndex++;

                    // Don't process notes that occur before the phrase
                    if (moonNote.tick < moonPhrase.tick)
                    {
                        YargLogger.LogFormatDebug("Vocals note at {0} does not exist within a phrase!", moonNote.tick);
                        continue;
                    }

                    // Handle lyric events
                    var lyricFlags = LyricSymbolFlags.None;
                    while (moonTextIndex < moonChart.events.Count)
                    {
                        var moonEvent = moonChart.events[moonTextIndex];
                        if (moonEvent.tick > moonNote.tick)
                            break;
                        moonTextIndex++;

                        string eventText = moonEvent.text;
                        // Non-lyric events
                        if (!eventText.StartsWith(TextEvents.LYRIC_PREFIX_WITH_SPACE))
                            continue;

                        var lyric = eventText.AsSpan()
                            .Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length).TrimStartAscii();
                        // Ignore empty lyrics
                        if (lyric.IsEmpty)
                            continue;

                        ProcessLyric(lyrics, lyric, moonEvent.tick, out lyricFlags);
                    }

                    // Create new note
                    var note = CreateVocalNote(moonNote, harmonyPart, lyricFlags);
                    if ((lyricFlags & LyricSymbolFlags.PitchSlide) != 0)
                    {
                        if (previousNote is not null)
                        {
                            previousNote.AddChildNote(note);
                            continue;
                        }

                        // Previous note is not in current phrase, add to existing carried note if it exists
                        if (carriedNote is not null)
                        {
                            carriedNote.AddChildNote(note);
                            continue;
                        }

                        // Previous note did not cross phrase boundary, but we are sliding across the boundary
                        if (phrases.Count == 0)
                        {
                            // Must be a charting error, continue
                            continue;
                        }

                        // Add to the previous parent note, making previous parent a carried note
                        if (previousParentLyric is not null)
                        {
                            previousParentLyric.AddChildNote(note);

                            carriedNote ??= previousParentLyric;

                            continue;
                        }
                    }

                    if (moonNote.tick + moonNote.length > moonPhrase.tick + moonPhrase.length)
                    {
                        carriedNote = note.ParentOrSelf;
                    }

                    notes.Add(note);
                    previousNote = note;
                    previousParentLyric = note.ParentOrSelf.Type == VocalNoteType.Lyric ? note.ParentOrSelf : previousParentLyric;
                }

                if (notes.Count < 1 && carriedNote == null)
                {
                    // This can occur on harmonies, HARM1 must contain phrases for all harmony parts
                    // so, for example, phrases with only HARM2/3 notes will cause this
                    continue;
                }

                var vocalsPhrase = CreateVocalsPhrase(moonPhrase, phraseTracker, notes, lyrics);
                phrases.Add(vocalsPhrase);
            }

            phrases.TrimExcess();
            if (staticLyricPhrases)
            {
                phrases.ForEach(FixLyricLengths);
            }

            return phrases;
        }

        /// <summary>
        /// Finds the vocal note that is probably the note associated with this lyric, based on the smallest time difference.
        /// </summary>
        /// <param name="lyric">The lyric to match against.</param>
        /// <param name="notes">The list of vocal notes in the phrase.</param>
        /// <returns>The found vocal note, or null if no notes were provided</returns>
        private static VocalNote? GetProbableNoteForLyric(LyricEvent lyric, List<VocalNote> notes)
        {
            // Get the note with the smallest time difference from the lyric
            var smallestTimeDelta = double.MaxValue;
            VocalNote? probableNote = null;

            foreach (var note in notes)
            {
                var timeDelta = Math.Abs(lyric.Time - note.Time);
                if (timeDelta < smallestTimeDelta)
                {
                    smallestTimeDelta = timeDelta;
                    probableNote = note;
                }
            }

            return probableNote;
        }

        /// <summary>
        /// Attempts to provide lengths for lyrics events in a phrase by associating them with vocal notes.
        /// </summary>
        /// <param name="phrase">The phrase containing the lyrics and vocal notes.</param>
        private static void FixLyricLengths(VocalsPhrase phrase)
        {
            for (var i = 0; i < phrase.Lyrics.Count; i++)
            {
                var lyric = phrase.Lyrics[i];
                var note = GetProbableNoteForLyric(lyric, phrase.PhraseParentNote.ChildNotes);
                if (note != null)
                {
                    lyric.TimeLength = note.TotalTimeEnd - note.Time;
                    lyric.TickLength = note.TotalTickEnd - note.Tick;
                }
                else
                {
                    YargLogger.LogFormatWarning(
                        "Could not find a note for lyric '{0}' at tick {1} in phrase at tick {2}",
                        lyric.Text, lyric.Tick, phrase.Tick);
                }
            }
        }

        private static VocalsPhrase MergePhrasePair(VocalsPhrase mainPhrase, VocalsPhrase otherPhrase)
        {
            var mergedLyrics = new List<LyricEvent>();
            var mergedLyricIdx = 0;

            for (var mainLyricIdx = 0; mainLyricIdx < mainPhrase.Lyrics.Count; mainLyricIdx++)
            {
                var mainLyric = mainPhrase.Lyrics[mainLyricIdx];

                // Handle any merged lyrics that happened before the current main lyric
                while (mergedLyricIdx < otherPhrase.Lyrics.Count)
                {
                    if (otherPhrase.Lyrics[mergedLyricIdx].Tick >= mainLyric.Tick)
                    {
                        break;
                    }

                    mergedLyrics.Add(otherPhrase.Lyrics[mergedLyricIdx++]);
                }

                // If there's a simultaneous syllable in the merged part...
                if (mergedLyricIdx < otherPhrase.Lyrics.Count &&
                    otherPhrase.Lyrics[mergedLyricIdx].Tick == mainLyric.Tick)
                {
                    var simultaneousMergedLyric = otherPhrase.Lyrics[mergedLyricIdx++];
                    // ...and their texts match...
                    if (string.Equals(simultaneousMergedLyric.Text, mainLyric.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        // ...add the longer of the two lyrics
                        mergedLyrics.Add(simultaneousMergedLyric.TimeLength > mainLyric.TimeLength ? simultaneousMergedLyric : mainLyric);
                    }
                    // ...otherwise, if its text isn't an exact match to the main syllable...
                    else
                    {
                        // ...add it immediately after the main syllable
                        mergedLyrics.Add(mainLyric);
                        mergedLyrics.Add(simultaneousMergedLyric);
                    }
                }
                else
                {
                    // ...if there is not a simultaneous syllable, add the main lyric
                    mergedLyrics.Add(mainLyric);
                }
            }

            // Handle any remaining merged lyrics after the last main phrase lyric
            while (mergedLyricIdx < otherPhrase.Lyrics.Count)
            {
                mergedLyrics.Add(otherPhrase.Lyrics[mergedLyricIdx++]);
            }

            mainPhrase.PhraseParentNote.AddChildNote(otherPhrase.PhraseParentNote.Clone());

            return new VocalsPhrase(
                mainPhrase.Time,
                mainPhrase.TimeLength,
                mainPhrase.Tick,
                mainPhrase.TickLength,
                mainPhrase.PhraseParentNote.Clone(),
                mergedLyrics
            );
        }

        private static List<VocalsPhrase> MergePhrases(List<VocalsPhrase> mainPhrases, List<VocalsPhrase> otherPhrases)
        {
            var result = new List<VocalsPhrase>();
            var otherIdx = 0;

            foreach (var mainPhrase in mainPhrases)
            {
                // Emit any other-only phrases that come before this main phrase
                while (otherIdx < otherPhrases.Count && otherPhrases[otherIdx].Tick < mainPhrase.Tick)
                {
                    result.Add(otherPhrases[otherIdx++]);
                }

                // Merge phrases at the same tick, otherwise emit main phrase alone
                if (otherIdx < otherPhrases.Count && otherPhrases[otherIdx].Tick == mainPhrase.Tick)
                {
                    result.Add(MergePhrasePair(mainPhrase, otherPhrases[otherIdx++]));
                }
                else
                {
                    result.Add(mainPhrase);
                }
            }

            // Emit any remaining other-only phrases
            while (otherIdx < otherPhrases.Count)
            {
                result.Add(otherPhrases[otherIdx++]);
            }

            return result;
        }

        private void SplitStaticLyricPhrases(ref List<VocalsPhrase> phrases, List<MoonPhrase> staticShiftPhrases)
        {
            var staticShifts = new List<uint>(staticShiftPhrases.Count);
            foreach (var p in staticShiftPhrases)
            {
                staticShifts.Add(p.tick);
            }

            foreach (var phrase in phrases)
            {
                foreach (var lyric in phrase.Lyrics)
                {
                    if ((lyric.Flags & LyricSymbolFlags.StaticShift) != 0)
                    {
                        staticShifts.Add(lyric.TickEnd);
                    }
                }
            }

            staticShifts.Sort();

            var finalPhrases = new List<VocalsPhrase>(phrases.Count * 2);
            int currentShiftIndex = 0;

            foreach (var phrase in phrases)
            {
                if (TrySplitByStaticShift(phrase, staticShifts, ref currentShiftIndex, out var staticSplits))
                {
                    finalPhrases.AddRange(staticSplits);
                    continue;
                }

                var timeSplits = SplitPhraseByTime(phrase);

                foreach (var timeSplitPhrase in timeSplits)
                {
                    finalPhrases.AddRange(SplitPhraseByCharacterCount(timeSplitPhrase));
                }
            }

            finalPhrases.RemoveAll(phrase => phrase.Lyrics.Count == 0);
            phrases = finalPhrases;
        }

        private bool TrySplitByStaticShift(VocalsPhrase phrase, List<uint> staticShifts,
            ref int currentShiftIndex, out List<VocalsPhrase> splitPhrases)
        {
            splitPhrases = null!;

            if (staticShifts.Count == 0 || currentShiftIndex >= staticShifts.Count)
            {
                return false;
            }

            var shift = staticShifts[currentShiftIndex];

            while (shift < phrase.Tick)
            {
                currentShiftIndex++;
                if (currentShiftIndex >= staticShifts.Count)
                {
                    return false;
                }

                shift = staticShifts[currentShiftIndex];
            }

            if (shift >= phrase.TickEnd)
            {
                return false;
            }

            splitPhrases = new List<VocalsPhrase>();
            var lastTick = phrase.Tick;
            var lastTime = phrase.Time;

            while (currentShiftIndex < staticShifts.Count)
            {
                shift = staticShifts[currentShiftIndex];
                if (shift >= phrase.TickEnd) break;

                splitPhrases.Add(CreateSubPhraseByTick(phrase, lastTime, lastTick, _moonSong.TickToTime(shift), shift));

                lastTick = shift;
                lastTime = _moonSong.TickToTime(shift);
                currentShiftIndex++;
            }

            splitPhrases.Add(CreateSubPhraseByTick(phrase, lastTime, lastTick, phrase.TimeEnd, phrase.TickEnd));
            return true;
        }

        private static List<VocalsPhrase> SplitPhraseByTime(VocalsPhrase phrase)
        {
            const double minTimeForGap = 0.6f;

            if (phrase.Lyrics.Count == 0)
            {
                return new List<VocalsPhrase>
                {
                    phrase
                };
            }

            var resultPhrases = new List<VocalsPhrase>();
            var sliceStartTime = phrase.Time;
            var sliceStartTick = phrase.Tick;
            int sliceStartIndex = 0;

            LyricEvent previousLyric = phrase.Lyrics[0];
            for (int i = 1; i < phrase.Lyrics.Count; i++)
            {
                var lyric = phrase.Lyrics[i];

                if (lyric.Time - previousLyric.TimeEnd > minTimeForGap && !previousLyric.JoinOrHyphenateWithNext && (i - sliceStartIndex) >= 3)
                {
                    int count = i - sliceStartIndex;
                    resultPhrases.Add(CreateSubPhraseByIndex(phrase, sliceStartIndex, count, sliceStartTime,
                        sliceStartTick, lyric.Time, lyric.Tick));

                    sliceStartTime = lyric.Time;
                    sliceStartTick = lyric.Tick;
                    sliceStartIndex = i;
                }

                previousLyric = lyric;
            }

            if (sliceStartIndex == 0)
            {
                return new List<VocalsPhrase>
                {
                    phrase
                };
            }

            int finalCount = phrase.Lyrics.Count - sliceStartIndex;
            resultPhrases.Add(CreateSubPhraseByIndex(phrase, sliceStartIndex, finalCount, sliceStartTime,
                sliceStartTick, phrase.TimeEnd, phrase.TickEnd));

            return resultPhrases;
        }

        private static List<VocalsPhrase> SplitPhraseByCharacterCount(VocalsPhrase phrase, int maxCharCap = 50)
        {
            if (phrase.Lyrics.Count == 0)
            {
                return new List<VocalsPhrase>
                {
                    phrase
                };
            }

            int totalCharacters = 0;
            foreach (var lyric in phrase.Lyrics)
            {
                totalCharacters += lyric.Text?.Length ?? 0;
            }

            if (totalCharacters <= maxCharCap)
            {
                return new List<VocalsPhrase>
                {
                    phrase
                };
            }

            int numChunks = (int) Math.Ceiling(totalCharacters / (maxCharCap * 0.75));
            int targetChunkSize = (int) Math.Ceiling((double) totalCharacters / numChunks);

            var resultPhrases = new List<VocalsPhrase>();
            int currentChunkLength = 0;

            var sliceStartTime = phrase.Time;
            var sliceStartTick = phrase.Tick;
            int sliceStartIndex = 0;

            for (int i = 0; i < phrase.Lyrics.Count; i++)
            {
                var lyric = phrase.Lyrics[i];
                currentChunkLength += lyric.Text?.Length ?? 0;

                bool isLastLyric = (i == phrase.Lyrics.Count - 1);
                bool canSplitHere = !lyric.JoinOrHyphenateWithNext;

                if (!isLastLyric && canSplitHere && currentChunkLength >= targetChunkSize)
                {
                    var nextLyric = phrase.Lyrics[i + 1];
                    int count = (i + 1) - sliceStartIndex;

                    resultPhrases.Add(CreateSubPhraseByIndex(phrase, sliceStartIndex, count, sliceStartTime,
                        sliceStartTick, nextLyric.Time, nextLyric.Tick));

                    sliceStartTime = nextLyric.Time;
                    sliceStartTick = nextLyric.Tick;
                    sliceStartIndex = i + 1;
                    currentChunkLength = 0;
                }
            }

            int finalCount = phrase.Lyrics.Count - sliceStartIndex;
            resultPhrases.Add(CreateSubPhraseByIndex(phrase, sliceStartIndex, finalCount, sliceStartTime,
                sliceStartTick, phrase.TimeEnd, phrase.TickEnd));

            return resultPhrases;
        }

        private static VocalsPhrase CreateSubPhraseByTick(VocalsPhrase source, double startTime, uint startTick,
            double endTime, uint endTick)
        {
            int startIndex = -1;
            int count = 0;
            for (int i = 0; i < source.Lyrics.Count; i++)
            {
                var lyric = source.Lyrics[i];
                if (lyric.Tick >= startTick && lyric.Tick < endTick)
                {
                    if (startIndex == -1) startIndex = i;
                    count++;
                }
                else if (lyric.Tick >= endTick)
                {
                    break;
                }
            }

            if (startIndex == -1)
            {
                YargLogger.LogFormatWarning("No lyrics found in split of phrase at tick {0} between ticks {1} and {2}.",
                    source.Tick, startTick, endTick);
            }

            return CreateSubPhraseByIndex(source, Math.Max(0, startIndex), count, startTime, startTick, endTime,
                endTick);
        }

        private static VocalsPhrase CreateSubPhraseByIndex(VocalsPhrase source, int startIndex, int count,
            double startTime, uint startTick, double endTime, uint endTick)
        {
            var lyricsSubset = count == 0 ? new List<LyricEvent>() : source.Lyrics.GetRange(startIndex, count);
            return new VocalsPhrase(
                startTime, endTime - startTime,
                startTick, endTick - startTick,
                source.PhraseParentNote,
                lyricsSubset);
        }

        private void TrimOrphanPhrases(List<VocalsPhrase> vocalPhrases, List<Phrase> otherPhrases)
        {
            int vocalPhraseIndex = 0;

            foreach (var otherPhrase in otherPhrases.ToList())
            {
                while (vocalPhraseIndex < vocalPhrases.Count
                    && vocalPhrases[vocalPhraseIndex].Tick < otherPhrase.Tick)
                {
                    vocalPhraseIndex++;
                }

                if (vocalPhraseIndex >= vocalPhrases.Count
                    || vocalPhrases[vocalPhraseIndex].Tick > otherPhrase.Tick)
                {
                    // No match found.
                    otherPhrases.Remove(otherPhrase);
                }

                // Otherwise, match found. Keep the other phrase.
            }
        }

        private void ProcessLyric(List<LyricEvent> lyrics, ReadOnlySpan<char> lyric, uint lyricTick, out LyricSymbolFlags lyricFlags)
        {
            LyricSymbols.DeferredLyricJoinWorkaround(lyrics, ref lyric, addHyphen: true);

            // Handle lyric modifiers
            lyricFlags = LyricSymbols.GetLyricFlags(lyric);

            const LyricSymbolFlags noteTypeMask = LyricSymbolFlags.NonPitched | LyricSymbolFlags.PitchSlide;
            if ((lyricFlags & noteTypeMask) == noteTypeMask)
            {
                YargLogger.LogFormatDebug("Lyric '{0}' at tick {1} specifies multiple lyric types! Flags: {2}", lyric.ToString(), lyricTick, lyricFlags);

                // TODO: Should we prefer one over the other?
                // lyricFlags &= ~LyricFlags.NonPitched;
                // lyricFlags &= ~LyricFlags.PitchSlide;
            }

            // Strip special symbols from lyrics
            string strippedLyric = LyricSymbols.StripForVocals(lyric.ToString());
            if (string.IsNullOrWhiteSpace(strippedLyric))
                return;

            double time = _moonSong.TickToTime(lyricTick);
            lyrics.Add(new(lyricFlags, strippedLyric, time, lyricTick));
        }

        private List<VocalsRangeShift> GetRangeShifts(List<VocalsPart> parts, MoonSong.MoonInstrument sourceInstrument)
        {
            var ranges = new List<VocalsRangeShift>();

            if (parts.All((part) => part.NotePhrases.Count < 1))
            {
                // No phrases; add a dummy default range
                ranges.Add(new(48, 72, 0, 0, 0, 0));
                return ranges;
            }

            double shiftLength = 0;
            uint shiftStartTick = 0;

            int phraseIndex = 0;
            var indexes = new List<(int phraseIndex, int noteIndex)>(parts.Select((_) => (0, 0)));
            var chart = _moonSong.GetChart(sourceInstrument, MoonSong.Difficulty.Expert);
            foreach (var moonEvent in chart.events)
            {
                for (; phraseIndex < chart.specialPhrases.Count; phraseIndex++)
                {
                    var phrase = chart.specialPhrases[phraseIndex];
                    if (phrase.tick >= moonEvent.tick)
                        break;

                    if (phrase.type == MoonPhrase.Type.Vocals_RangeShift)
                    {
                        // Commit active shift
                        AddPitchRange(shiftStartTick, phrase.tick, shiftLength);

                        // Start new shift
                        shiftStartTick = phrase.tick;
                        double shiftStart = _moonSong.TickToTime(phrase.tick);
                        double shiftEnd = _moonSong.TickToTime(phrase.tick + phrase.length);
                        shiftLength = shiftEnd - shiftStart;
                    }
                }

                var eventText = moonEvent.text;
                if (eventText.StartsWith("range_shift"))
                {
                    // Commit active shift
                    AddPitchRange(shiftStartTick, moonEvent.tick, shiftLength);

                    // Start new shift
                    shiftStartTick = moonEvent.tick;

                    // Two forms: [range_shift] and [range_shift 0.5]
                    // The latter specifies the time of the shift in seconds
                    eventText.SplitOnce(' ', out var time);
                    if (time.IsEmpty || !double.TryParse(time, out shiftLength))
                        shiftLength = 0;
                }
                else if (eventText.StartsWith(TextEvents.LYRIC_PREFIX_WITH_SPACE))
                {
                    var lyric = eventText.AsSpan()
                        .Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length).TrimStartAscii();
                    // Ignore empty lyrics
                    if (lyric.IsEmpty)
                        continue;

                    // Check for the range shift symbol
                    var flags = LyricSymbols.GetLyricFlags(lyric);
                    if ((flags & LyricSymbolFlags.RangeShift) != 0)
                    {
                        // Commit active shift
                        AddPitchRange(shiftStartTick, moonEvent.tick, shiftLength);

                        // Start new shift
                        shiftStartTick = moonEvent.tick;
                        shiftLength = 0;
                    }
                }
            }

            // Finish off last range
            AddPitchRange(shiftStartTick, uint.MaxValue, shiftLength);

            // If a song is all talkies, there will be no ranges added
            // so we add a dummy range here
            if (ranges.Count < 1)
                ranges.Add(new(48, 72, 0, 0, 0, 0));

            return ranges;

            void AddPitchRange(uint startTick, uint endTick, double shiftLength)
            {
                // Determine pitch bounds for this range shift
                float minPitch = float.MaxValue;
                float maxPitch = float.MinValue;

                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    var (phraseIndex, noteIndex) = indexes[i];

                    for (; phraseIndex < part.NotePhrases.Count; phraseIndex++)
                    {
                        var phrase = part.NotePhrases[phraseIndex];
                        if (phrase.Tick >= endTick)
                            break;

                        if (phrase.TickEnd < startTick || phrase.IsPercussion)
                            // TODO: Percussion phrases should probably stop the range and start a new one afterwards
                            continue;

                        for (; noteIndex < phrase.PhraseParentNote.ChildNotes.Count; noteIndex++)
                        {
                            var note = phrase.PhraseParentNote.ChildNotes[noteIndex];
                            if (note.Tick >= endTick || note.TickEnd < startTick)
                                break;

                            foreach (var child in note.AllNotes)
                            {
                                if (child.Tick >= endTick || child.TickEnd < startTick || child.IsNonPitched)
                                    continue;

                                minPitch = Math.Min(minPitch, child.Pitch);
                                maxPitch = Math.Max(maxPitch, child.Pitch);
                            }
                        }

                        // Manual end due to reaching the last note in the range
                        if (noteIndex < phrase.PhraseParentNote.ChildNotes.Count)
                            break;

                        noteIndex = 0;
                    }

                    indexes[i] = (phraseIndex, noteIndex);
                }

                if (minPitch == float.MaxValue || maxPitch == float.MinValue)
                    return;

                double startTime = _moonSong.TickToTime(startTick);
                endTick = _moonSong.TimeToTick(startTime + shiftLength);
                ranges.Add(new(minPitch, maxPitch, startTime, shiftLength, startTick, endTick - startTick));
            }
        }


        private VocalNote CreateVocalNote(MoonNote moonNote, int harmonyPart, LyricSymbolFlags lyricFlags)
        {
            var vocalType = GetVocalNoteType(moonNote);
            float pitch = GetVocalNotePitch(moonNote, lyricFlags);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new VocalNote(pitch, harmonyPart, vocalType, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private float GetVocalNotePitch(MoonNote moonNote, LyricSymbolFlags lyricFlags)
        {
            float pitch = moonNote.vocalsPitch + (0.01f * _settings.TuningOffsetCents);

            // Unpitched/percussion notes
            if ((lyricFlags & LyricSymbolFlags.NonPitched) != 0 || (moonNote.flags & MoonNote.Flags.Vocals_Percussion) != 0)
                pitch = -1f;

            return pitch;
        }

        private VocalNoteType GetVocalNoteType(MoonNote moonNote)
        {
            var flags = VocalNoteType.Lyric;

            // Percussion notes
            if ((moonNote.flags & MoonNote.Flags.Vocals_Percussion) != 0)
            {
                flags = VocalNoteType.Percussion;
            }

            return flags;
        }

        private VocalsPhrase CreateVocalsPhrase(MoonPhrase moonPhrase, Dictionary<MoonPhrase.Type, MoonPhrase?> phrasetracker,
            List<VocalNote> notes, List<LyricEvent> lyrics)
        {
            double time = _moonSong.TickToTime(moonPhrase.tick);
            double timeLength = GetLengthInTime(moonPhrase);
            uint tick = moonPhrase.tick;
            uint tickLength = moonPhrase.length;

            var phraseFlags = GetVocalsPhraseFlags(moonPhrase, phrasetracker);
            var isPercussionPhrase = IsPhrasePercussion(notes);

            // Convert to MoonPhrase into a vocal note phrase
            var phraseNote = new VocalNote(phraseFlags, isPercussionPhrase, time, timeLength, tick, tickLength);
            foreach (var note in notes)
            {
                phraseNote.AddChildNote(note);
            }

            return new VocalsPhrase(time, timeLength, tick, tickLength, phraseNote, lyrics);
        }

        private NoteFlags GetVocalsPhraseFlags(MoonPhrase moonPhrase, Dictionary<MoonPhrase.Type, MoonPhrase?> phrasetracker)
        {
            var phraseFlags = NoteFlags.None;

            // No need to check the start of the phrase, as entering the function
            // already guarantees that condition *if* the below is true
            var starPower = phrasetracker[MoonPhrase.Type.Starpower];
            if (starPower != null && moonPhrase.tick < starPower.tick + starPower.length)
            {
                phraseFlags |= NoteFlags.StarPower;
            }

            return phraseFlags;
        }

        private bool IsPhrasePercussion(List<VocalNote> notes)
        {
            // Empty phrases can still be treated as vocal phrases; it doesn't really matter
            // Mixing percussion and non-percussion in a single phrase is garbage data, so we only need to check the first note
            if (notes.Count == 0 || !notes[0].IsPercussion)
            {
                return false;
            }

            return true;
        }
    }
}