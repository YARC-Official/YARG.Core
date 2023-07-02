using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

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

            var notePhrases = GetVocalsPhrases(moonChart, harmonyPart);
            var otherPhrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            return new(notePhrases, otherPhrases, textEvents);
        }

        private List<VocalsPhrase> GetVocalsPhrases(MoonChart moonChart, int harmonyPart)
        {
            var phrases = new List<VocalsPhrase>(moonChart.specialPhrases.Count);
            var currentPhrases = new Dictionary<SpecialPhrase.Type, SpecialPhrase>();

            int moonNoteIndex = 0;
            int moonTextIndex = 0;

            // Load relative to special phrases, not notes
            for (int moonPhraseIndex = 0; moonPhraseIndex < moonChart.specialPhrases.Count; moonPhraseIndex++)
            {
                var moonPhrase = moonChart.specialPhrases[moonPhraseIndex];
                currentPhrases[moonPhrase.type] = moonPhrase;
                
                if (moonPhrase.type != SpecialPhrase.Type.Vocals_LyricPhrase)
                    continue;

                // Ensure any other phrases on the same tick get tracked
                moonPhraseIndex++;
                while (moonPhraseIndex < moonChart.specialPhrases.Count)
                {
                    var moonPhrase2 = moonChart.specialPhrases[moonPhraseIndex];
                    if (moonPhrase2.tick > moonPhrase.tick)
                        break;

                    currentPhrases[moonPhrase2.type] = moonPhrase2;
                    moonPhraseIndex++;
                }

                // Go through each note and lyric in the phrase
                var notes = new List<VocalNote>();
                var lyrics = new List<TextEvent>();
                VocalNote previousNote = null;
                while (moonNoteIndex < moonChart.notes.Count)
                {
                    var moonNote = moonChart.notes[moonNoteIndex];
                    if (moonNote.tick >= (moonPhrase.tick + moonPhrase.length))
                        break;
                    moonNoteIndex++;

                    // Don't process notes that occur before the phrase
                    if (moonNote.tick < moonPhrase.tick)
                    {
                        Debug.WriteLine($"Vocals note at {moonNote.tick} does not exist within a phrase!");
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

                        // Ignore non-lyric events
                        if (!moonEvent.eventName.StartsWith(ChartIOHelper.LYRIC_EVENT_PREFIX))
                            continue;

                        string lyric = moonEvent.eventName.Replace(ChartIOHelper.LYRIC_EVENT_PREFIX, "");

                        // Only process note modifiers for lyrics that match the current note
                        if (moonEvent.tick == moonNote.tick)
                        {
                            // Handle modifier lyrics
                            if (lyric.EndsWith(PITCH_SLIDE_CHARACTER))
                                lyricType = LyricType.PitchSlide;
                            else if (NONPITCHED_CHARACTERS.Contains(lyric[^1]))
                                lyricType = LyricType.NonPitched;
                        }

                        lyrics.Add(new(lyric, moonEvent.time, moonEvent.tick));
                    }

                    // Create new note
                    var note = CreateVocalNote(moonNote, currentPhrases, harmonyPart, lyricType);
                    if (lyricType is LyricType.PitchSlide && previousNote is not null)
                    {
                        previousNote.AddChildNote(note);
                        continue;
                    }

                    notes.Add(note);
                    previousNote = note;
                }

                var vocalsPhrase = CreateVocalsPhrase(moonPhrase, currentPhrases, notes, lyrics);
                phrases.Add(vocalsPhrase);
            }

            phrases.TrimExcess();
            return phrases;
        }

        private VocalNote CreateVocalNote(MoonNote moonNote, Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases,
            int harmonyPart, LyricType lyricType)
        {
            var vocalType = GetVocalNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            float pitch = GetVocalNotePitch(moonNote, lyricType);

            return new VocalNote(pitch, harmonyPart, vocalType, generalFlags,
                moonNote.time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
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
                flags |= VocalNoteType.Percussion;

            return flags;
        }

        private VocalsPhrase CreateVocalsPhrase(SpecialPhrase moonPhrase,
            Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases, List<VocalNote> notes, List<TextEvent> lyrics)
        {
            var type = GetVocalsPhraseType(moonPhrase, notes);
            var bounds = GetVocalsPhraseBounds(moonPhrase);
            var phraseFlags = GetVocalsPhraseFlags(moonPhrase, currentPhrases);

            return new VocalsPhrase(type, bounds, phraseFlags, notes, lyrics);
        }

        private ChartEvent GetVocalsPhraseBounds(SpecialPhrase moonPhrase)
        {
            return new Phrase(PhraseType.LyricPhrase, moonPhrase.time, GetLengthInTime(moonPhrase),
                moonPhrase.tick, moonPhrase.length);
        }

        private VocalsPhraseType GetVocalsPhraseType(SpecialPhrase moonPhrase, List<VocalNote> notes)
        {
            var firstNote = notes[0];
            var phraseType = firstNote.Type switch
            {
                VocalNoteType.Lyric      => VocalsPhraseType.Lyric,
                VocalNoteType.Percussion => VocalsPhraseType.Percussion,
                _ => throw new NotImplementedException($"Unhandled vocal note type {firstNote.Type}!")
            };

            // Some debug verifications
            Debug.Assert(notes.All((note) => note.Type == firstNote.Type), "Vocals phrase with inconsistent note types! Must be either lyric or percussion, but found both");

            // Modify Moonscraper phrase to have the correct type
            moonPhrase.type = phraseType switch
            {
                VocalsPhraseType.Lyric      => SpecialPhrase.Type.Vocals_LyricPhrase,
                VocalsPhraseType.Percussion => SpecialPhrase.Type.Vocals_PercussionPhrase,
                _ => throw new NotImplementedException($"Unhandled vocal phrase type {phraseType}!")
            };

            return phraseType;
        }

        private VocalsPhraseFlags GetVocalsPhraseFlags(SpecialPhrase moonPhrase,
            Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var phraseFlags = VocalsPhraseFlags.None;

            // Star power
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.Starpower, out var starPower) && IsEventInPhrase(moonPhrase, starPower))
                phraseFlags |= VocalsPhraseFlags.StarPower;

            return phraseFlags;
        }
    }
}