using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

namespace YARG.Core.Chart
{
    using CurrentPhrases = Dictionary<SpecialPhrase.Type, SpecialPhrase>;

    /// <summary>
    /// Loads chart data from a MoonSong.
    /// </summary>
    internal partial class MoonSongLoader : ISongLoader
    {
        private delegate TNote CreateNoteDelegate<TNote>(MoonNote moonNote, CurrentPhrases currentPhrases)
            where TNote : Note;

        private MoonSong _moonSong;

        public void LoadSong(string filePath)
        {
            _moonSong = Path.GetExtension(filePath).ToLower() switch
            {
                ".mid" => MidReader.ReadMidi(filePath),
                ".chart" => ChartReader.ReadChart(filePath),
                _ => throw new ArgumentException($"Unrecognized file extension for chart path '{filePath}'!", nameof(filePath))
            };
        }

        public void LoadMidi(MidiFile midi)
        {
            _moonSong = MidReader.ReadMidi(midi);
        }

        public void LoadDotChart(string chartText)
        {
            _moonSong = ChartReader.ReadChart(new StringReader(chartText));
        }

        public InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument instrument)
        {
            // TODO
            return new(instrument);
        }

        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument)
        {
            // TODO
            return new(instrument);
        }

        public InstrumentTrack<VocalNote> LoadVocalsTrack(Instrument instrument)
        {
            // TODO
            return new(instrument);
        }

        private InstrumentDifficulty<TNote> LoadDifficulty<TNote>(Instrument instrument, Difficulty difficulty,
            CreateNoteDelegate<TNote> createNote)
            where TNote : Note
        {
            var moonChart = GetMoonChart(instrument, difficulty);
            var notes = GetNotes(moonChart, difficulty, createNote);
            var phrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            return new(instrument, difficulty, notes, phrases, textEvents);
        }

        private List<TNote> GetNotes<TNote>(MoonChart moonChart, Difficulty difficulty, CreateNoteDelegate<TNote> createNote)
            where TNote : Note
        {
            var notes = new List<TNote>(moonChart.notes.Count);

            int moonPhraseIndex = 0;
            // Phrases stored here are *not* guaranteed to be active, as it's simpler that way
            // We need to check the phrase bounds anyways, which is very simple to do
            var currentPhrases = new CurrentPhrases();

            foreach (var moonNote in moonChart.notes)
            {
                // Keep track of active special phrases
                while (moonPhraseIndex < moonChart.specialPhrases.Count)
                {
                    var moonPhrase = moonChart.specialPhrases[moonPhraseIndex];
                    if (moonPhrase.tick > moonNote.tick)
                        break;

                    currentPhrases[moonPhrase.type] = moonPhrase;
                    moonPhraseIndex++;
                }

                // Skip Expert+ notes if not on Expert+
                if (difficulty != Difficulty.ExpertPlus && (moonNote.flags & MoonNote.Flags.InstrumentPlus) != 0)
                    continue;

                var newNote = createNote(moonNote, currentPhrases);
                AddNoteToList(notes, newNote);
            }

            notes.TrimExcess();
            return notes;
        }

        private List<Phrase> GetPhrases(MoonChart moonChart)
        {
            var phrases = new List<Phrase>(moonChart.specialPhrases.Count);
            foreach (var moonPhrase in moonChart.specialPhrases)
            {
                var phraseType = moonPhrase.type switch
                {
                    SpecialPhrase.Type.Starpower           => PhraseType.StarPower,
                    SpecialPhrase.Type.Solo                => PhraseType.Solo,
                    SpecialPhrase.Type.Versus_Player1      => PhraseType.VersusPlayer1,
                    SpecialPhrase.Type.Versus_Player2      => PhraseType.VersusPlayer2,
                    SpecialPhrase.Type.TremoloLane         => PhraseType.TremoloLane,
                    SpecialPhrase.Type.TrillLane           => PhraseType.TrillLane,
                    SpecialPhrase.Type.ProDrums_Activation => PhraseType.DrumFill,
                    SpecialPhrase.Type.Vocals_LyricPhrase  => PhraseType.LyricPhrase,
                    _ => throw new NotImplementedException($"Unhandled special phrase type {moonPhrase.type}!")
                };

                // TODO: Detect vocals percussion phrases

                var newPhrase = new Phrase(phraseType, moonPhrase.time, GetLengthInTime(moonPhrase), moonPhrase.tick, moonPhrase.length);
                phrases.Add(newPhrase);
            }

            return phrases;
        }

        private List<TextEvent> GetTextEvents(MoonChart moonChart)
        {
            var textEvents = new List<TextEvent>(moonChart.events.Count);
            foreach (var moonText in moonChart.events)
            {
                var newText = new TextEvent(moonText.eventName, moonText.time, moonText.tick);
                textEvents.Add(newText);
            }

            return textEvents;
        }

        private NoteFlags GetGeneralFlags(MoonNote moonNote, CurrentPhrases currentPhrases)
        {
            var flags = NoteFlags.None;

            // Star power
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.Starpower, out var starPower) && IsNoteInPhrase(moonNote, starPower))
            {
                flags |= NoteFlags.StarPower;

                if (!IsNoteInPhrase(moonNote.PreviousSeperateMoonNote, starPower))
                    flags |= NoteFlags.StarPowerStart;

                if (!IsNoteInPhrase(moonNote.NextSeperateMoonNote, starPower))
                    flags |= NoteFlags.StarPowerEnd;
            }

            // Solos
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.Solo, out var solo) && IsNoteInPhrase(moonNote, solo))
            {
                if (!IsNoteInPhrase(moonNote.PreviousSeperateMoonNote, solo))
                    flags |= NoteFlags.SoloStart;

                if (!IsNoteInPhrase(moonNote.NextSeperateMoonNote, solo))
                    flags |= NoteFlags.SoloEnd;
            }

            return flags;
        }

        private void AddNoteToList<TNote>(List<TNote> notes, TNote note)
            where TNote : Note
        {
            // The parent of all notes on the current tick
            var currentParent = notes.Count > 0 ? notes[^1] : null;
            // Previous parent note (on a different tick)
            var previousParent = notes.Count > 1 ? notes[^2] : null;

            // Determine if this is part of a chord
            if (note.Tick == currentParent?.Tick)
            {
                // Same chord, assign previous and add as child
                note.previousNote = previousParent;
                currentParent.AddChildNote(note);
                return;
            }

            // New chord
            previousParent = currentParent;
            currentParent = note;

            // Assign next/previous note references
            if (previousParent is not null)
            {
                previousParent.nextNote = currentParent;
                foreach (var child in previousParent.ChildNotes)
                    child.nextNote = currentParent;

                currentParent.previousNote = previousParent;
            }

            notes.Add(note);
        }

        private double GetLengthInTime(double startTime, uint tick, uint tickLength)
        {
            return _moonSong.TickToTime(tick + tickLength, _moonSong.resolution) - startTime;
        }

        private double GetLengthInTime(MoonNote note)
        {
            return GetLengthInTime(note.time, note.tick, note.length);
        }

        private double GetLengthInTime(SpecialPhrase phrase)
        {
            return GetLengthInTime(phrase.time, phrase.tick, phrase.length - 1);
        }

        private static bool IsNoteInPhrase(MoonNote note, SpecialPhrase phrase)
        {
            return note.tick >= phrase.tick && note.tick < (phrase.tick + phrase.length);
        }

        private MoonChart GetMoonChart(Instrument instrument, Difficulty difficulty)
        {
            var moonInstrument = YargInstrumentToMoonInstrument(instrument);
            var moonDifficulty = YargDifficultyToMoonDifficulty(difficulty);
            return _moonSong.GetChart(moonInstrument, moonDifficulty);
        }

        private static MoonSong.MoonInstrument YargInstrumentToMoonInstrument(Instrument instrument) => instrument switch
        {
            Instrument.FiveFretGuitar     => MoonSong.MoonInstrument.Guitar,
            Instrument.FiveFretCoopGuitar => MoonSong.MoonInstrument.GuitarCoop,
            Instrument.FiveFretBass       => MoonSong.MoonInstrument.Bass,
            Instrument.FiveFretRhythm     => MoonSong.MoonInstrument.Rhythm,
            Instrument.Keys               => MoonSong.MoonInstrument.Keys,

            Instrument.SixFretGuitar     => MoonSong.MoonInstrument.GHLiveGuitar,
            Instrument.SixFretCoopGuitar => MoonSong.MoonInstrument.GHLiveBass,
            Instrument.SixFretBass       => MoonSong.MoonInstrument.GHLiveRhythm,
            Instrument.SixFretRhythm     => MoonSong.MoonInstrument.GHLiveCoop,

            Instrument.FourLaneDrums or
            Instrument.FiveLaneDrums or
            Instrument.ProDrums => MoonSong.MoonInstrument.Drums,

            Instrument.ProGuitar_17Fret => MoonSong.MoonInstrument.ProGuitar_17Fret,
            Instrument.ProGuitar_22Fret => MoonSong.MoonInstrument.ProGuitar_22Fret,
            Instrument.ProBass_17Fret   => MoonSong.MoonInstrument.ProBass_17Fret,
            Instrument.ProBass_22Fret   => MoonSong.MoonInstrument.ProBass_22Fret,

            // Instrument.ProKeys => MoonSong.MoonInstrument.ProKeys,

            // Vocals and harmony need to be handled specially
            // Instrument.Vocals  => MoonSong.MoonInstrument.Vocals,
            // Instrument.Harmony => MoonSong.MoonInstrument.Harmony1,

            _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
        };

        private static MoonSong.Difficulty YargDifficultyToMoonDifficulty(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy       => MoonSong.Difficulty.Easy,
            Difficulty.Medium     => MoonSong.Difficulty.Medium,
            Difficulty.Hard       => MoonSong.Difficulty.Hard,
            Difficulty.Expert or
            Difficulty.ExpertPlus => MoonSong.Difficulty.Expert,
            _ => throw new InvalidOperationException($"Invalid difficulty {difficulty}!")
        };
    }
}