using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Extensions;

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
            return new(instrument, parts, ranges);
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
            return new(instrument, parts, ranges);
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
                    if ((lyricFlags & LyricSymbolFlags.PitchSlide) != 0 && previousNote is not null)
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

        private void ProcessLyric(List<LyricEvent> lyrics, ReadOnlySpan<char> lyric, uint lyricTick,
            out LyricSymbolFlags lyricFlags)
        {
            LyricSymbols.DeferredLyricJoinWorkaround(lyrics, ref lyric, addHyphen: true);

            // Handle lyric modifiers
            lyricFlags = LyricSymbols.GetLyricFlags(lyric);

            const LyricSymbolFlags noteTypeMask = LyricSymbolFlags.NonPitched | LyricSymbolFlags.PitchSlide;
            if ((lyricFlags & noteTypeMask) == noteTypeMask)
            {
                YargTrace.DebugWarning($"Lyric '{lyric.ToString()}' at tick {lyricTick} specifies multiple lyric types! Flags: {lyricFlags}");

                // TODO: Should we prefer one over the other?
                // lyricFlags &= ~LyricFlags.NonPitched;
                // lyricFlags &= ~LyricFlags.PitchSlide;
            }

            // Strip special symbols from lyrics
            string strippedLyric = LyricSymbols.StripForVocals(lyric.ToString());
            if (string.IsNullOrWhiteSpace(strippedLyric))
            {
                lyricFlags = LyricSymbolFlags.None;
                return;
            }

            double time = _moonSong.TickToTime(lyricTick);
            lyrics.Add(new(lyricFlags, strippedLyric, time, lyricTick));
        }

        private List<VocalsPitchRange> GetRangeShifts(List<VocalsPart> parts, MoonSong.MoonInstrument sourceInstrument)
        {
            var ranges = new List<VocalsPitchRange>();

            // Do nothing if no phrases were parsed in
            if (parts.All((part) => part.NotePhrases.Count < 1))
                return ranges;

            uint shiftStartTick = 0;

            var indexes = new List<(int phraseIndex, int noteIndex)>(parts.Select((_) => (0, 0)));
            var chart = _moonSong.GetChart(sourceInstrument, MoonSong.Difficulty.Expert);
            foreach (var moonEvent in chart.events)
            {
                var eventText = moonEvent.text;
                if (eventText == "range_shift")
                {
                    AddPitchRange(moonEvent.tick);
                    continue;
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
                        AddPitchRange(moonEvent.tick);
                        break;
                    }
                }
            }

            // Finish off last range
            AddPitchRange(uint.MaxValue);
            return ranges;

            void AddPitchRange(uint endTick)
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

                        if (phrase.TickEnd < shiftStartTick || phrase.IsPercussion)
                            // TODO: Percussion phrases should probably stop the range and start a new one afterwards
                            continue;

                        for (; noteIndex < phrase.PhraseParentNote.ChildNotes.Count; noteIndex++)
                        {
                            var note = phrase.PhraseParentNote.ChildNotes[noteIndex];
                            if (note.Tick >= endTick)
                                break;

                            foreach (var child in note.ChordEnumerator())
                            {
                                if (note.TickEnd < shiftStartTick || note.IsNonPitched)
                                    continue;

                                minPitch = Math.Min(minPitch, note.Pitch);
                                maxPitch = Math.Max(maxPitch, note.Pitch);
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

                ranges.Add(new(minPitch, maxPitch, _moonSong.TickToTime(shiftStartTick), shiftStartTick));
                shiftStartTick = endTick;
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
            float pitch = moonNote.vocalsPitch;

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