using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Extensions;

// TODO: Better parsing/sanitization of lyric events

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private enum LyricType
        {
            None = 0,

            NonPitched = 1,
            PitchSlide = 2,
        }

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

            var shifts = GetRangeShifts(parts[0]);
            return new(instrument, parts, shifts);
        }

        private VocalsTrack LoadHarmonyVocals(Instrument instrument)
        {
            var parts = new List<VocalsPart>()
            {
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony1),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony2),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony3),
            };

            var shifts = GetRangeShifts(parts[0]);
            return new(instrument, parts, shifts);
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
            var notePhrases = GetVocalsPhrases(moonChart, harmonyPart);
            var otherPhrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            return new(isHarmony, notePhrases, otherPhrases, textEvents);
        }

        private List<VocalsPhrase> GetVocalsPhrases(MoonChart moonChart, int harmonyPart)
        {
            var phrases = new List<VocalsPhrase>();

            // Prefill with the valid phrases
            var phraseTracker = new Dictionary<MoonPhrase.Type, MoonPhrase?>()
            {
                { MoonPhrase.Type.Starpower , null },
                { MoonPhrase.Type.Versus_Player1 , null },
                { MoonPhrase.Type.Versus_Player2 , null },
                { MoonPhrase.Type.Vocals_PercussionPhrase , null },
            };

            int moonNoteIndex = 0;
            int moonTextIndex = 0;

            for (int moonPhraseIndex = 0; moonPhraseIndex < moonChart.specialPhrases.Count;)
            {
                var moonPhrase = moonChart.specialPhrases[moonPhraseIndex++];
                if (moonPhrase.type != MoonPhrase.Type.Vocals_LyricPhrase)
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
                        YargTrace.DebugWarning($"Vocals note at {moonNote.tick} does not exist within a phrase!");
                        continue;
                    }

                    // Handle lyric events
                    var lyricType = LyricType.None;
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

                        ProcessLyric(lyrics, lyric, ref lyricType, moonEvent.tick, moonNote.tick);
                    }

                    // Create new note
                    var note = CreateVocalNote(moonNote, harmonyPart, lyricType);
                    if (lyricType is LyricType.PitchSlide && previousNote is not null)
                    {
                        previousNote.AddChildNote(note);
                        continue;
                    }

                    notes.Add(note);
                    previousNote = note;
                }

                if (notes.Count < 1)
                {
                    // This can occur on harmonies, HARM1 must contain phrases for all harmony parts
                    // so, for example, phrases with only HARM2/3 notes will cause this
                    continue;
                }

                var vocalsPhrase = CreateVocalsPhrase(moonPhrase, phraseTracker, notes, lyrics);
                phrases.Add(vocalsPhrase);
            }

            phrases.TrimExcess();
            return phrases;
        }

        private void ProcessLyric(List<LyricEvent> lyrics, ReadOnlySpan<char> lyric, ref LyricType lyricType,
            uint lyricTick, uint noteTick)
        {
            // Workaround for a certain set of badly-formatted vocal tracks which place the hyphen
            // for pitch bend lyrics on the pitch bend and not the lyric itself
            // Not really necessary except to ensure the lyric displays correctly
            if (lyrics.Count > 0 && !lyrics[^1].Text.EndsWith('-') &&
                (lyric.Equals("+-", StringComparison.Ordinal) || lyric.Equals("-+", StringComparison.Ordinal)))
            {
                var other = lyrics[^1];
                lyrics[^1] = new(other.Flags, $"{other.Text}-", other.Time, other.Tick);
                lyric = "+";
            }

            // Handle lyric modifiers
            var lyricFlags = LyricFlags.None;
            for (var modifiers = lyric; !modifiers.IsEmpty; modifiers = modifiers[..^1])
            {
                char modifier = modifiers[^1];
                if (!LyricSymbols.ALL_SYMBOLS.Contains(modifier))
                    break;

                if (modifier == LyricSymbols.STATIC_SHIFT_SYMBOL)
                    lyricFlags |= LyricFlags.StaticShift;

                // Only process note modifiers for lyrics that match the current note
                if (lyricTick == noteTick)
                {
                    LyricType type;
                    if (modifier == LyricSymbols.PITCH_SLIDE_SYMBOL)
                        type = LyricType.PitchSlide;
                    else if (LyricSymbols.NONPITCHED_SYMBOLS.Contains(modifier))
                        type = LyricType.NonPitched;
                    else
                        continue;

                    if (lyricType != LyricType.None && type != lyricType)
                    {
                        YargTrace.DebugWarning($"Lyric '{lyric.ToString()}' at tick {lyricTick} specifies multiple lyric types ({lyricType} and {type})!");
                        continue;
                    }
                }
            }

            if (lyric[0] == LyricSymbols.HARMONY_HIDE_SYMBOL)
                lyricFlags |= LyricFlags.HarmonyHidden;

            // Strip special symbols from lyrics
            string strippedLyric = LyricSymbols.StripForVocals(lyric.ToString());
            if (string.IsNullOrWhiteSpace(strippedLyric))
                return;

            double time = _moonSong.TickToTime(lyricTick);
            lyrics.Add(new(lyricFlags, strippedLyric, time, lyricTick));
        }

        private List<VocalsRangeShift> GetRangeShifts(VocalsPart referencePart)
        {
            var shifts = new List<VocalsRangeShift>();

            if (referencePart.NotePhrases.Count < 0)
                return shifts;

            int partPhraseIndex = 0;
            int nextNoteIndex = 0;
            var chart = _moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);
            foreach (var moonEvent in chart.events)
            {
                // Track the currently active or upcoming phrase
                while (partPhraseIndex < referencePart.NotePhrases.Count)
                {
                    var phrase = referencePart.NotePhrases[partPhraseIndex];
                    if (phrase.TickEnd > moonEvent.tick)
                        break;
                    partPhraseIndex++;
                    nextNoteIndex = 0;
                }
                var currentPhrase = referencePart.NotePhrases[partPhraseIndex];

                // Track the upcoming note
                while (nextNoteIndex < currentPhrase.PhraseParentNote.ChildNotes.Count)
                {
                    var note = currentPhrase.PhraseParentNote.ChildNotes[nextNoteIndex];
                    if (note.Tick > moonEvent.tick)
                        break;
                    nextNoteIndex++;
                }
                var nextNote = currentPhrase.PhraseParentNote.ChildNotes[nextNoteIndex];

                uint shiftEndTick = moonEvent.tick < currentPhrase.Tick
                    ? currentPhrase.Tick
                    : nextNote.Tick;

                var eventText = moonEvent.text;
                if (eventText == "range_shift")
                {
                    var shift = CreateRangeShift(moonEvent.tick, shiftEndTick);
                    shifts.Add(shift);
                    continue;
                }
                else if (eventText.StartsWith(TextEvents.LYRIC_PREFIX_WITH_SPACE))
                {
                    var lyric = eventText.AsSpan()
                        .Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length).TrimStartAscii();
                    // Ignore empty lyrics
                    if (lyric.IsEmpty)
                        continue;

                    // Search for the range shift symbol
                    for (var modifiers = lyric; !modifiers.IsEmpty; modifiers = modifiers[..^1])
                    {
                        char modifier = modifiers[^1];
                        if (!LyricSymbols.ALL_SYMBOLS.Contains(modifier))
                            break;

                        if (modifier == LyricSymbols.RANGE_SHIFT_SYMBOL)
                        {
                            var shift = CreateRangeShift(moonEvent.tick, shiftEndTick);
                            shifts.Add(shift);
                            break;
                        }
                    }
                }
            }

            return shifts;
        }

        private VocalsRangeShift CreateRangeShift(uint startTick, uint endTick)
        {
            double startTime = _moonSong.TickToTime(startTick);
            double endTime = _moonSong.TickToTime(endTick);

            // Limit range shift length to 1 second
            const double maxShiftTime = 1.0;
            if ((endTime - startTime) > maxShiftTime)
            {
                endTime = startTime + maxShiftTime;
                endTick = _moonSong.TimeToTick(endTime);
            }

            return new(startTime, endTime - startTime, startTick, endTick - startTick);
        }

        private VocalNote CreateVocalNote(MoonNote moonNote, int harmonyPart, LyricType lyricType)
        {
            var vocalType = GetVocalNoteType(moonNote);
            float pitch = GetVocalNotePitch(moonNote, lyricType);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new VocalNote(pitch, harmonyPart, vocalType, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private float GetVocalNotePitch(MoonNote moonNote, LyricType lyricType)
        {
            float pitch = moonNote.vocalsPitch;

            // Unpitched/percussion notes
            if (lyricType is LyricType.NonPitched || (moonNote.flags & MoonNote.Flags.Vocals_Percussion) != 0)
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

            // Convert to MoonPhrase into a vocal note phrase
            var phraseNote = new VocalNote(phraseFlags, time, timeLength, tick, tickLength);
            foreach (var note in notes)
            {
                phraseNote.AddNoteToPhrase(note);
            }

            return new VocalsPhrase(time, timeLength, tick, tickLength, phraseNote, lyrics);
        }

        private NoteFlags GetVocalsPhraseFlags(MoonPhrase moonPhrase, Dictionary<MoonPhrase.Type, MoonPhrase?> phrasetracker)
        {
            var phraseFlags = NoteFlags.None;

            // No need to check the start of the phrase, as entering the function
            // already guarantees that condition *if* the below is true
            var starPower = phrasetracker[MoonPhrase.Type.Starpower];
            if (starPower != null &&  moonPhrase.tick < starPower.tick + starPower.length)
            {
                phraseFlags |= NoteFlags.StarPower;
            }

            return phraseFlags;
        }
    }
}