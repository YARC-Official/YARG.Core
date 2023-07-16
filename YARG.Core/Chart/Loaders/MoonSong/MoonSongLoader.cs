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
            where TNote : Note<TNote>;

        private MoonSong _moonSong;
        private ParseSettings _settings;

        public void LoadSong(ParseSettings settings, string filePath)
        {
            _settings = settings;
            _moonSong = Path.GetExtension(filePath).ToLower() switch
            {
                ".mid" => MidReader.ReadMidi(filePath),
                ".chart" => ChartReader.ReadChart(filePath),
                _ => throw new ArgumentException($"Unrecognized file extension for chart path '{filePath}'!", nameof(filePath))
            };
        }

        public void LoadMidi(ParseSettings settings, MidiFile midi)
        {
            _settings = settings;
            _moonSong = MidReader.ReadMidi(midi);
        }

        public void LoadDotChart(ParseSettings settings, string chartText)
        {
            _settings = settings;
            _moonSong = ChartReader.ReadChart(new StringReader(chartText));
        }

        public void CompleteMetadata(SongMetadata metadata)
        {
            static int LoadInteger(int existing, int newValue)
            {
                if (existing < 0)
                    return newValue;

                return existing;
            }

            static double LoadDouble(double existing, double newValue)
            {
                if (existing < 0.0)
                    return newValue;

                return existing;
            }

            static string LoadString(string existing, string newString)
            {
                if (string.IsNullOrWhiteSpace(existing))
                    return newString;

                return existing;
            }

            metadata.Name = LoadString(metadata.Name, _moonSong.metaData.name);
            metadata.Artist = LoadString(metadata.Artist, _moonSong.metaData.artist);
            metadata.Album = LoadString(metadata.Album, _moonSong.metaData.album);
            metadata.Genre = LoadString(metadata.Genre, _moonSong.metaData.genre);
            metadata.Year = LoadString(metadata.Year, _moonSong.metaData.year);
            metadata.Charter = LoadString(metadata.Charter, _moonSong.metaData.charter);

            metadata.PreviewStart = LoadDouble(metadata.PreviewStart, _moonSong.metaData.previewStart);
            metadata.PreviewEnd = LoadDouble(metadata.PreviewEnd, _moonSong.metaData.previewEnd);

            metadata.BandDifficulty = LoadInteger(metadata.BandDifficulty, _moonSong.metaData.difficulty);
        }

        public List<TextEvent> LoadGlobalEvents()
        {
            var textEvents = new List<TextEvent>(_moonSong.events.Count);
            foreach (var moonText in _moonSong.events)
            {
                var newText = new TextEvent(moonText.title, moonText.time, moonText.tick);
                textEvents.Add(newText);
            }

            return textEvents;
        }

        public SyncTrack LoadSyncTrack()
        {
            var tempos = new List<TempoChange>(_moonSong.bpms.Count);
            var timeSigs = new List<TimeSignatureChange>(_moonSong.timeSignatures.Count);

            foreach (var moonBpm in _moonSong.bpms)
            {
                var tempo = new TempoChange(moonBpm.displayValue, moonBpm.time, moonBpm.tick);
                tempos.Add(tempo);
            }

            foreach (var moonTimeSig in _moonSong.timeSignatures)
            {
                var timeSig = new TimeSignatureChange(moonTimeSig.numerator, moonTimeSig.denominator,
                    moonTimeSig.time, moonTimeSig.tick);
                timeSigs.Add(timeSig);
            }

            return new((uint) _moonSong.resolution, tempos, timeSigs);
        }

        private InstrumentDifficulty<TNote> LoadDifficulty<TNote>(Instrument instrument, Difficulty difficulty,
            CreateNoteDelegate<TNote> createNote)
            where TNote : Note<TNote>
        {
            var moonChart = GetMoonChart(instrument, difficulty);
            var notes = GetNotes(moonChart, difficulty, createNote);
            var phrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            return new(instrument, difficulty, notes, phrases, textEvents);
        }

        private List<TNote> GetNotes<TNote>(MoonChart moonChart, Difficulty difficulty, CreateNoteDelegate<TNote> createNote)
            where TNote : Note<TNote>
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
                    SpecialPhrase.Type.Vocals_PercussionPhrase => PhraseType.PercussionPhrase,
                    _ => throw new NotImplementedException($"Unhandled special phrase type {moonPhrase.type}!")
                };

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
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.Starpower, out var starPower) && IsEventInPhrase(moonNote, starPower))
            {
                flags |= NoteFlags.StarPower;

                if (!IsEventInPhrase(moonNote.PreviousSeperateMoonNote, starPower))
                    flags |= NoteFlags.StarPowerStart;

                if (!IsEventInPhrase(moonNote.NextSeperateMoonNote, starPower))
                    flags |= NoteFlags.StarPowerEnd;
            }

            // Solos
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.Solo, out var solo) && IsEventInPhrase(moonNote, solo))
            {
                if (!IsEventInPhrase(moonNote.PreviousSeperateMoonNote, solo))
                    flags |= NoteFlags.SoloStart;

                if (!IsEventInPhrase(moonNote.NextSeperateMoonNote, solo))
                    flags |= NoteFlags.SoloEnd;
            }

            return flags;
        }

        private void AddNoteToList<TNote>(List<TNote> notes, TNote note)
            where TNote : Note<TNote>
        {
            // The parent of all notes on the current tick
            var currentParent = notes.Count > 0 ? notes[^1] : null;
            // Previous parent note (on a different tick)
            var previousParent = notes.Count > 1 ? notes[^2] : null;

            // Determine if this is part of a chord
            if (note.Tick == currentParent?.Tick)
            {
                // Same chord, assign previous and add as child
                note.PreviousNote = previousParent;
                currentParent.AddChildNote(note);
                return;
            }

            // New chord
            previousParent = currentParent;
            currentParent = note;

            // Assign next/previous note references
            if (previousParent is not null)
            {
                previousParent.NextNote = currentParent;
                foreach (var child in previousParent.ChildNotes)
                    child.NextNote = currentParent;

                currentParent.PreviousNote = previousParent;
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

        private static bool IsEventInPhrase(SongObject note, SpecialPhrase phrase)
        {
            return note.tick >= phrase.tick && note.tick < (phrase.tick + phrase.length);
        }

        private static bool IsNoteClosestToEndOfPhrase(MoonNote note, SpecialPhrase phrase)
        {
            int endTick = (int) (phrase.tick + phrase.length);

            // Find the note to compare against
            MoonNote otherNote;
            {
                var previousNote = note.PreviousSeperateMoonNote;
                var nextNote = note.NextSeperateMoonNote;

                if (IsEventInPhrase(note, phrase))
                {
                    // Note is in the phrase, check if this is the last note in the phrase
                    if (nextNote is not null && !IsEventInPhrase(nextNote, phrase))
                    {
                        // The phrase ends between the given note and the next note
                        otherNote = nextNote;
                    }
                    else
                    {
                        // This is either the last note in the chart, or not the last note of the phrase
                        return nextNote is null;
                    }
                }
                else
                {
                    // Note is not in the phrase, check if the previous note is the last in the phrase
                    if (previousNote is null)
                    {
                        // This is the first note in the chart, check by distance
                        float tickThreshold = note.song.resolution / 3; // 1/12th note
                        return Math.Abs((int) note.tick - endTick) < tickThreshold;
                    }
                    else if (note.tick > endTick && previousNote.tick < endTick)
                    {
                        // The phrase ends between the previous note and the given note
                        // IsEventInPhrase() is not used here since cases such as drum activations at the end of breaks
                        // can possibly make it so that neither the previous nor given note are in the phrase
                        otherNote = previousNote;
                    }
                    else
                    {
                        // The phrase is not applicable to the given note
                        return false;
                    }
                }
            }

            // Compare the distance of each note
            // If the distances are equal, the previous note wins
            int currentDistance = Math.Abs((int) note.tick - endTick);
            int otherDistance = Math.Abs((int) otherNote.tick - endTick);
            return currentDistance < otherDistance || (currentDistance == otherDistance && note.tick < otherNote.tick);
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