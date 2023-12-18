using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Utility;

// TODO: Better parsing/sanitization of lyric events

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private enum LyricType
        {
            None = 0,

            NonPitched = 1 << 0,
            PitchSlide = 1 << 1,
        }

        private const string PITCH_SLIDE_CHARACTER = "+";

        private static readonly List<char> NONPITCHED_CHARACTERS = new()
        {
            '#', // Standard
            '^', // Lenient
            '*', // Unknown, present in some charts
        };

        private static readonly Dictionary<string, string> LYRIC_REPLACE_MAP = new()
        {
            { "+", "" },
            { "#", "" },
            { "^", "" },
            { "*", "" },
            { "_", " " },
        };

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
            return new(instrument, parts);
        }

        private VocalsTrack LoadHarmonyVocals(Instrument instrument)
        {
            var parts = new List<VocalsPart>()
            {
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony1),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony2),
                LoadVocalsPart(MoonSong.MoonInstrument.Harmony3),
            };
            return new(instrument, parts);
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
            var phraseTracker = new Dictionary<SpecialPhrase.Type, SpecialPhrase?>()
            {
                { SpecialPhrase.Type.Starpower , null },
                { SpecialPhrase.Type.Versus_Player1 , null },
                { SpecialPhrase.Type.Versus_Player2 , null },
                { SpecialPhrase.Type.Vocals_PercussionPhrase , null },
            };

            int moonNoteIndex = 0;
            int moonTextIndex = 0;

            for (int moonPhraseIndex = 0; moonPhraseIndex < moonChart.specialPhrases.Count;)
            {
                var moonPhrase = moonChart.specialPhrases[moonPhraseIndex++];
                if (moonPhrase.type != SpecialPhrase.Type.Vocals_LyricPhrase)
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
                var lyrics = new List<TextEvent>();
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

                        var splitter = moonEvent.text.AsSpan().Split(' ');
                        // Ignore non-lyric events
                        var start = splitter.GetNext();
                        var lyric = splitter.Remaining;
                        if (!start.Equals(TextEventDefinitions.LYRIC_PREFIX, StringComparison.Ordinal))
                            continue;

                        // Only process note modifiers for lyrics that match the current note
                        if (moonEvent.tick == moonNote.tick)
                        {
                            // Handle modifier lyrics
                            if (lyric.EndsWith(PITCH_SLIDE_CHARACTER))
                                lyricType = LyricType.PitchSlide;
                            else if (lyric.Length > 0 && NONPITCHED_CHARACTERS.Contains(lyric[^1]))
                                lyricType = LyricType.NonPitched;
                        }

                        double time = _moonSong.TickToTime(moonEvent.tick);
                        lyrics.Add(new(lyric.ToString(), time, moonEvent.tick));
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

        private VocalsPhrase CreateVocalsPhrase(SpecialPhrase moonPhrase, Dictionary<SpecialPhrase.Type, SpecialPhrase?> phrasetracker,
            List<VocalNote> notes, List<TextEvent> lyrics)
        {
            var bounds = GetVocalsPhraseBounds(moonPhrase);
            var phraseFlags = GetVocalsPhraseFlags(moonPhrase, phrasetracker);

            // Convert to SpecialPhrase into a vocal note phrase
            var phraseNote = new VocalNote(phraseFlags, bounds.Time, bounds.TimeLength,
                bounds.Tick, bounds.TickLength);
            foreach (var note in notes)
            {
                phraseNote.AddNoteToPhrase(note);
            }

            return new VocalsPhrase(bounds, phraseNote, lyrics);
        }

        private Phrase GetVocalsPhraseBounds(SpecialPhrase moonPhrase)
        {
            double time = _moonSong.TickToTime(moonPhrase.tick);
            return new Phrase(PhraseType.LyricPhrase, time, GetLengthInTime(moonPhrase),
                moonPhrase.tick, moonPhrase.length);
        }

        private NoteFlags GetVocalsPhraseFlags(SpecialPhrase moonPhrase, Dictionary<SpecialPhrase.Type, SpecialPhrase?> phrasetracker)
        {
            var phraseFlags = NoteFlags.None;

            // No need to check the start of the phrase, as entering the function
            // already guarantees that condition *if* the below is true
            var starPower = phrasetracker[SpecialPhrase.Type.Starpower];
            if (starPower != null &&  moonPhrase.tick < starPower.tick + starPower.length)
            {
                phraseFlags |= NoteFlags.StarPower;
            }

            return phraseFlags;
        }
    }
}