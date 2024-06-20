﻿using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The chart data for a song.
    /// </summary>
    public class SongChart
    {
        public uint Resolution => SyncTrack.Resolution;

        public List<TextEvent> GlobalEvents { get; set; } = new();
        public List<Section> Sections { get; set; } = new();

        public SyncTrack SyncTrack { get; set; } = new();
        public VenueTrack VenueTrack { get; set; } = new();
        public LyricsTrack Lyrics { get; set; } = new();

        public InstrumentTrack<GuitarNote> FiveFretGuitar { get; set; } = new(Instrument.FiveFretGuitar);
        public InstrumentTrack<GuitarNote> FiveFretCoop { get; set; } = new(Instrument.FiveFretCoopGuitar);
        public InstrumentTrack<GuitarNote> FiveFretRhythm { get; set; } = new(Instrument.FiveFretRhythm);
        public InstrumentTrack<GuitarNote> FiveFretBass { get; set; } = new(Instrument.FiveFretBass);
        public InstrumentTrack<GuitarNote> Keys { get; set; } = new(Instrument.Keys);

        public IEnumerable<InstrumentTrack<GuitarNote>> FiveFretTracks
        {
            get
            {
                yield return FiveFretGuitar;
                yield return FiveFretCoop;
                yield return FiveFretRhythm;
                yield return FiveFretBass;
                yield return Keys;
            }
        }

        // Not supported yet
        public InstrumentTrack<GuitarNote> SixFretGuitar { get; set; } = new(Instrument.SixFretGuitar);
        public InstrumentTrack<GuitarNote> SixFretCoop { get; set; } = new(Instrument.SixFretCoopGuitar);
        public InstrumentTrack<GuitarNote> SixFretRhythm { get; set; } = new(Instrument.SixFretRhythm);
        public InstrumentTrack<GuitarNote> SixFretBass { get; set; } = new(Instrument.SixFretBass);

        public IEnumerable<InstrumentTrack<GuitarNote>> SixFretTracks
        {
            get
            {
                yield return SixFretGuitar;
                yield return SixFretCoop;
                yield return SixFretRhythm;
                yield return SixFretBass;
            }
        }

        public InstrumentTrack<DrumNote> FourLaneDrums { get; set; } = new(Instrument.FourLaneDrums);
        public InstrumentTrack<DrumNote> ProDrums { get; set; } = new(Instrument.ProDrums);
        public InstrumentTrack<DrumNote> FiveLaneDrums { get; set; } = new(Instrument.FiveLaneDrums);

        // public InstrumentTrack<DrumNote> TrueDrums { get; set; } = new(Instrument.TrueDrums);

        public IEnumerable<InstrumentTrack<DrumNote>> DrumsTracks
        {
            get
            {
                yield return FourLaneDrums;
                yield return ProDrums;
                yield return FiveLaneDrums;
            }
        }

        public InstrumentTrack<ProGuitarNote> ProGuitar_17Fret { get; set; } = new(Instrument.ProGuitar_17Fret);
        public InstrumentTrack<ProGuitarNote> ProGuitar_22Fret { get; set; } = new(Instrument.ProGuitar_22Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_17Fret { get; set; } = new(Instrument.ProBass_17Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_22Fret { get; set; } = new(Instrument.ProBass_22Fret);

        public IEnumerable<InstrumentTrack<ProGuitarNote>> ProGuitarTracks
        {
            get
            {
                yield return ProGuitar_17Fret;
                yield return ProGuitar_22Fret;
                yield return ProBass_17Fret;
                yield return ProBass_22Fret;
            }
        }

        // public InstrumentTrack<ProKeysNote> ProKeys { get; set; } = new(Instrument.ProKeys);

        public VocalsTrack Vocals { get; set; } = new(Instrument.Vocals);
        public VocalsTrack Harmony { get; set; } = new(Instrument.Harmony);

        public IEnumerable<VocalsTrack> VocalsTracks
        {
            get
            {
                yield return Vocals;
                yield return Harmony;
            }
        }

        // public InstrumentTrack<DjNote> Dj { get; set; } = new(Instrument.Dj);

        // To explicitly allow creation without going through a file
        public SongChart() { }

        internal SongChart(ISongLoader loader)
        {
            GlobalEvents = loader.LoadGlobalEvents();
            SyncTrack = loader.LoadSyncTrack();
            VenueTrack = loader.LoadVenueTrack();
            Sections = loader.LoadSections();
            Lyrics = loader.LoadLyrics();

            FiveFretGuitar = loader.LoadGuitarTrack(Instrument.FiveFretGuitar);
            FiveFretCoop = loader.LoadGuitarTrack(Instrument.FiveFretCoopGuitar);
            FiveFretRhythm = loader.LoadGuitarTrack(Instrument.FiveFretRhythm);
            FiveFretBass = loader.LoadGuitarTrack(Instrument.FiveFretBass);
            Keys = loader.LoadGuitarTrack(Instrument.Keys);

            SixFretGuitar = loader.LoadGuitarTrack(Instrument.SixFretGuitar);
            SixFretCoop = loader.LoadGuitarTrack(Instrument.SixFretCoopGuitar);
            SixFretRhythm = loader.LoadGuitarTrack(Instrument.SixFretRhythm);
            SixFretBass = loader.LoadGuitarTrack(Instrument.SixFretBass);

            FourLaneDrums = loader.LoadDrumsTrack(Instrument.FourLaneDrums);
            ProDrums = loader.LoadDrumsTrack(Instrument.ProDrums);
            FiveLaneDrums = loader.LoadDrumsTrack(Instrument.FiveLaneDrums);

            // TrueDrums = loader.LoadDrumsTrack(Instrument.TrueDrums);

            ProGuitar_17Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_17Fret);
            ProGuitar_22Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_22Fret);
            ProBass_17Fret = loader.LoadProGuitarTrack(Instrument.ProBass_17Fret);
            ProBass_22Fret = loader.LoadProGuitarTrack(Instrument.ProBass_22Fret);

            // ProKeys = loader.LoadProKeysTrack(Instrument.ProKeys);

            Vocals = loader.LoadVocalsTrack(Instrument.Vocals);
            Harmony = loader.LoadVocalsTrack(Instrument.Harmony);

            // Dj = loader.LoadDjTrack(Instrument.Dj);

            PostProcessSections();
            FixDrumPhraseEnds();

            // Ensure beatlines are present
            if (SyncTrack.Beatlines is null or { Count: < 1 })
            {
                SyncTrack.GenerateBeatlines(GetLastTick());
            }

            // Use beatlines to place auto-generated drum activation phrases
            GenerateDrumActivationPhrases();
        }

        private void PostProcessSections()
        {
            uint lastTick = GetLastTick();

            // If there are no sections in the chart, auto-generate some sections.
            // This prevents issues with songs with no sections, such as in practice mode.
            if (Sections.Count == 0)
            {
                const int AUTO_GEN_SECTION_COUNT = 10;
                ReadOnlySpan<double> factors = stackalloc double[AUTO_GEN_SECTION_COUNT]{
                    0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0
                };

                uint startTick = 0;
                double startTime = SyncTrack.TickToTime(0);

                for (int i = 0; i < AUTO_GEN_SECTION_COUNT; i++)
                {
                    uint endTick = (uint)(lastTick * factors[i]);
                    double endTime = SyncTrack.TickToTime(endTick);

                    // "0% - 10%", "10% - 20%", etc.
                    var sectionName = $"{i * 10}% - {i + 1}0%";

                    var section = new Section(sectionName, startTime, startTick)
                    {
                        TickLength = endTick - startTick,
                        TimeLength = endTime - startTime,
                    };

                    Sections.Add(section);

                    // Set the start of the next section to the end of this one
                    startTick = endTick;
                    startTime = endTime;
                }
            }
            else
            {
                // Otherwise make sure the length of the last section is correct
                var lastSection = Sections[^1];
                lastSection.TickLength = lastTick - lastSection.Tick;
                lastSection.TimeLength = SyncTrack.TickToTime(lastTick) - lastSection.Time;
            }
        }

        private void FixDrumPhraseEnds()
        {
            foreach (var drumTrack in new List<InstrumentTrack<DrumNote>> { ProDrums, FiveLaneDrums, FourLaneDrums })
            {
                FixDrumPhraseEnds(drumTrack, n => n.IsSoloEnd, NoteFlags.SoloEnd);
                FixDrumPhraseEnds(drumTrack, n => n.IsStarPowerEnd, NoteFlags.StarPowerEnd);
            }
        }

        private static void FixDrumPhraseEnds(InstrumentTrack<DrumNote> drumTrack, Predicate<DrumNote> isPhraseEnd,
            NoteFlags phraseEndFlag)
        {
            if (!drumTrack.TryGetDifficulty(Difficulty.ExpertPlus, out var trackExpertPlus))
            {
                return;
            }

            if (!drumTrack.TryGetDifficulty(Difficulty.Expert, out var trackExpert))
            {
                return;
            }

            var notesExpertPlus = trackExpertPlus.Notes;
            var notesExpert = trackExpert.Notes;

            var phraseEndsExpertPlus = notesExpertPlus
                .Where(n => isPhraseEnd(n)).ToArray();
            var phraseEndsExpert = notesExpert
                .Where(n => isPhraseEnd(n)).ToArray();

            if (phraseEndsExpertPlus.Length <= phraseEndsExpert.Length)
            {
                return;
            }

            var i = 1;
            foreach (var phraseEndExpertPlus in phraseEndsExpertPlus)
            {
                while (i < notesExpert.Count && notesExpert[i].Tick <= phraseEndExpertPlus.Tick)
                {
                    i++;
                }

                var phraseEndExpert = notesExpert[i - 1];
                if (!isPhraseEnd(phraseEndExpert))
                {
                    phraseEndExpert.ActivateFlag(phraseEndFlag);
                }
            }
        }

        private void GenerateDrumActivationPhrases()
        {
            foreach (var drumTrack in new List<InstrumentTrack<DrumNote>> { ProDrums, FiveLaneDrums, FourLaneDrums })
            {
                foreach (Difficulty difficultyType in Enum.GetValues(typeof(Difficulty)))
                {
                    if (drumTrack.TryGetDifficulty(difficultyType, out var thisDifficultyTrack))
                    {
                        GenerateDrumActivationPhrases(thisDifficultyTrack);
                    }
                }
            }
        }

        private void GenerateDrumActivationPhrases(InstrumentDifficulty<DrumNote> difficulty)
        {
            if (difficulty.Notes.Count == 0){
                return;
            }

            uint lastNoteTick = difficulty.Notes.GetLastTick();

            // Automatically add in Star Power activation phrases for drum charts that don't have enough

            var starPowerPhrases = new List<Phrase>();
            var soloPhrases = new List<Phrase>();

            foreach (var thisPhrase in difficulty.Phrases)
            {
                switch (thisPhrase.Type)
                {
                    case PhraseType.DrumFill:
                        // Assume that any drum chart with manually placed fill phrases will have enough. Stop here
                        YargLogger.LogDebug("Prevented generating Activation phrases for a Drum chart that already has them");
                        return;
                    
                    case PhraseType.StarPower:
                        starPowerPhrases.Add(thisPhrase);
                        break;
                    
                    case PhraseType.Solo:
                        soloPhrases.Add(thisPhrase);
                        break;
                }
            }

            // Activation cannot occur before the player has enough SP to activate
            // Start processing after the 2nd SP phrase
            
            if (starPowerPhrases.Count < 2)
            {
                YargLogger.LogDebug("Cannot generate Activation phrases for Drum chart. Not enough Star Power phrases available.");
                return;
            }

            double prevReferenceTime = starPowerPhrases[1].TimeEnd;

            // Align activation phrases with measure boundaries that have already been evaluated
            var measureBeatLines = SyncTrack.Beatlines.Where(x => x.Type == BeatlineType.Measure).ToList();

            int currentMeasureIndex = measureBeatLines.GetIndexOfPrevious(prevReferenceTime);
            if (currentMeasureIndex == -1)
            {
                return;
            }

            int totalMeasures = measureBeatLines.Count;
            
            var allTimeSigs = SyncTrack.TimeSignatures;
            var currentTimeSigIndex = allTimeSigs.GetIndexOfPrevious(prevReferenceTime);

            if (currentTimeSigIndex == -1) {
                return;
            } 
            
            var currentTimeSig = allTimeSigs[currentTimeSigIndex];
            int timeSigMeasureIndex = measureBeatLines.GetIndexOfPrevious(currentTimeSig.Tick);

            // Limits for placing activation phrases (in seconds)
            const float MIN_SPACING_TIME = 2;
            const float MAX_SPACING_TIME = 10;

            do
            {
                int measuresPerActivator = 4;
                if (currentMeasureIndex + measuresPerActivator > totalMeasures) break;

                // Start by moving forward 4 measures from the last iteration
                // If that is too long of a wait at the current tempo/time signature do 2 measures instead
                if (measureBeatLines[currentMeasureIndex].Time - prevReferenceTime > MAX_SPACING_TIME)
                {
                    measuresPerActivator -= 2;
                }

                currentMeasureIndex += measuresPerActivator;

                // Activator notes should only fall on bar lines that are multiples of measuresPerActivator
                // from the last time signature change
                int measureRemainder = (currentMeasureIndex - timeSigMeasureIndex) % measuresPerActivator;
                if (measureRemainder > 0) currentMeasureIndex += measureRemainder;

                // Reached the end of the chart
                if (currentMeasureIndex >= totalMeasures) break;

                var currentMeasureLine = measureBeatLines[currentMeasureIndex];

                int newTimeSigIndex = SyncTrack.TimeSignatures.GetIndexOfPrevious(currentMeasureLine.Tick);
                if (newTimeSigIndex > currentTimeSigIndex)
                {
                    // Moved forward into a new time signature
                    currentTimeSigIndex = newTimeSigIndex;
                    currentTimeSig = allTimeSigs[newTimeSigIndex];
                    timeSigMeasureIndex = measureBeatLines.GetIndexOfPrevious(currentTimeSig.Tick);

                    //move the activation note to the start of this time signature
                    currentMeasureIndex = timeSigMeasureIndex;
                    currentMeasureLine = measureBeatLines[currentMeasureIndex];
                }

                uint currentMeasureTick = currentMeasureLine.Tick;

                if (currentMeasureTick >= lastNoteTick) break;

                uint eighthNoteTickLength = currentTimeSig.GetTicksPerBeat(SyncTrack) / 2;
                
                // Attempt to retrieve an activation note directly on the bar line
                var activationNote = difficulty.Notes.GetPrevious(currentMeasureTick);

                // No more notes in this chart
                if (activationNote == null) break;

                // No note exists on or near the bar line, move on
                if (activationNote.Tick < currentMeasureTick - eighthNoteTickLength) continue;

                var nearestSPPhrase = starPowerPhrases.GetPrevious(currentMeasureTick);
                if (nearestSPPhrase == null) break;

                // Prevent activation phrases from appearing less than MIN_SPACING away from SP phrases
                prevReferenceTime = Math.Max(prevReferenceTime, nearestSPPhrase.TimeEnd);

                // This activation phrase is too close to an SP phrase or previous activation note
                if (currentMeasureLine.Time - prevReferenceTime < MIN_SPACING_TIME) continue;

                // Do not put an activation phrase here if it overlaps with an existing SP phrase
                if (nearestSPPhrase.TickEnd > currentMeasureTick) continue;

                // Prevent placing an activation phrase here if it overlaps with a solo section
                if (soloPhrases.Count > 0)
                {
                    var nearestSoloPhrase = soloPhrases.GetPrevious(currentMeasureTick);
                    if (nearestSoloPhrase != null && nearestSoloPhrase.TickEnd > currentMeasureTick) continue;
                }
                
                // Do not put an activation phrase here if there aren't enough notes to hit after activating SP
                const uint SP_MIN_NOTES = 16;
                int starPowerEndMeasureIndex = Math.Min(currentMeasureIndex + 4, totalMeasures - 1);
                uint starPowerEndTick = measureBeatLines[starPowerEndMeasureIndex].Tick;

                int totalNotesForStarPower = 0;
                var testNote = activationNote.NextNote;
                while (totalNotesForStarPower < SP_MIN_NOTES && testNote != null)
                {
                    if (testNote.Tick > starPowerEndTick) break;
                    
                    totalNotesForStarPower += testNote.ChildNotes.Count + 1;
                    testNote = testNote.NextNote;
                }

                if (totalNotesForStarPower < SP_MIN_NOTES) continue;

                // This is a good place to put an Activation note
                prevReferenceTime = currentMeasureLine.Time;

                if (!IsIdealDrumActivationNote(activationNote, difficulty.Instrument))
                {
                    // The note on the bar line doesn't contain a combo of crash and kick/snare
                    // Search +/- an 8th note to find a possible syncopated activation note
                    testNote = difficulty.Notes.GetNext(currentMeasureTick - eighthNoteTickLength - 1);
                    while (testNote != null && testNote.Tick <= currentMeasureTick + eighthNoteTickLength)
                    {
                        if (testNote != activationNote)
                        {
                            if (IsIdealDrumActivationNote(testNote, difficulty.Instrument))
                            {
                                activationNote = testNote;
                                break;
                            }
                        }

                        testNote = testNote.NextNote;
                    }
                }

                // Add the activator flag to all notes in this chord
                foreach (var note in activationNote.ChordEnumerator())
                {
                    note.DrumFlags |= DrumNoteFlags.StarPowerActivator;
                }

                // Mark the start of a drum fill phrase one measure before this bar line
                var previousMeasureLine = measureBeatLines[currentMeasureIndex - 1];
                uint fillPhraseStartTick = previousMeasureLine.Tick;
                uint fillPhraseEndTick = activationNote.Tick;
                double fillPhraseStartTime = previousMeasureLine.Time;
                double fillPhraseTimeLength = activationNote.Time - fillPhraseStartTime;

                var newDrumFillPhrase = new Phrase(PhraseType.DrumFill,
                                                   fillPhraseStartTime, fillPhraseTimeLength,
                                                   fillPhraseStartTick, fillPhraseEndTick - fillPhraseStartTick);

                int newPhraseIndex = difficulty.Phrases.GetIndexOfNext(fillPhraseStartTick);

                if (newPhraseIndex != -1)
                {
                    // Insert new activation phrase at the appopriate index
                    difficulty.Phrases.Insert(newPhraseIndex, newDrumFillPhrase);
                }
                else
                {
                    // Add new phrase to the end of the list
                    difficulty.Phrases.Add(newDrumFillPhrase);
                }

                YargLogger.LogFormatDebug("Generated a Drums SP Activation phrase from tick {0} to {1}", fillPhraseStartTick, fillPhraseEndTick);
            } while (currentMeasureIndex < totalMeasures);
        }

        public static bool IsIdealDrumActivationNote(DrumNote note, Instrument instrument)
        {
            bool containsCrash = false;
            bool containsKick = false;
            bool containsSnare = false;

            foreach (var childNote in note.ChordEnumerator())
            {
                var thisPad = childNote.Pad;
                if (instrument == Instrument.FiveLaneDrums)
                {
                    containsCrash |= thisPad == (int) FiveLaneDrumPad.Orange;
                    containsKick |= thisPad == (int) FiveLaneDrumPad.Kick;
                    containsSnare |= thisPad == (int) FiveLaneDrumPad.Red;
                }
                else
                {
                    if (instrument == Instrument.FourLaneDrums)
                    {
                        containsCrash |= thisPad == (int) FourLaneDrumPad.GreenDrum;
                    }
                    else
                    {
                        containsCrash |= thisPad == (int) FourLaneDrumPad.GreenCymbal;
                    }

                    containsSnare |= thisPad == (int) FourLaneDrumPad.RedDrum;
                    containsKick |= thisPad == (int) FourLaneDrumPad.Kick;
                }
            }

            return containsCrash && (containsKick || containsSnare);
        }

        public void Append(SongChart song)
        {
            if (!song.FiveFretGuitar.IsEmpty)
                FiveFretGuitar = song.FiveFretGuitar;

            if (!song.FiveFretCoop.IsEmpty)
                FiveFretCoop = song.FiveFretCoop;

            if (!song.FiveFretRhythm.IsEmpty)
                FiveFretRhythm = song.FiveFretRhythm;

            if (!song.FiveFretBass.IsEmpty)
                FiveFretBass = song.FiveFretBass;

            if (!song.Keys.IsEmpty)
                Keys = song.Keys;

            if (!song.SixFretGuitar.IsEmpty)
                SixFretGuitar = song.SixFretGuitar;

            if (!song.SixFretCoop.IsEmpty)
                SixFretCoop = song.SixFretCoop;

            if (!song.SixFretRhythm.IsEmpty)
                SixFretRhythm = song.SixFretRhythm;

            if (!song.SixFretBass.IsEmpty)
                SixFretBass = song.SixFretBass;

            if (!song.FourLaneDrums.IsEmpty)
                FourLaneDrums = song.FourLaneDrums;

            if (!song.ProDrums.IsEmpty)
                ProDrums = song.ProDrums;

            if (!song.FiveLaneDrums.IsEmpty)
                FiveLaneDrums = song.FiveLaneDrums;

            if (!song.ProGuitar_17Fret.IsEmpty)
                ProGuitar_17Fret = song.ProGuitar_17Fret;

            if (!song.ProGuitar_22Fret.IsEmpty)
                ProGuitar_22Fret = song.ProGuitar_22Fret;

            if (!song.ProBass_17Fret.IsEmpty)
                ProBass_17Fret = song.ProBass_17Fret;

            if (!song.ProBass_22Fret.IsEmpty)
                ProBass_22Fret = song.ProBass_22Fret;

            if (!song.Vocals.IsEmpty)
                Vocals = song.Vocals;

            if (!song.Harmony.IsEmpty)
                Harmony = song.Harmony;
        }

        public static SongChart FromFile(in ParseSettings settings, string filePath)
        {
            var loader = MoonSongLoader.LoadSong(settings, filePath);
            return new(loader);
        }

        public static SongChart FromMidi(in ParseSettings settings, MidiFile midi)
        {
            var loader = MoonSongLoader.LoadMidi(settings, midi);
            return new(loader);
        }

        public static SongChart FromDotChart(in ParseSettings settings, string chartText)
        {
            var loader = MoonSongLoader.LoadDotChart(settings, chartText);
            return new(loader);
        }

        public InstrumentTrack<GuitarNote> GetFiveFretTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar,
                Instrument.FiveFretCoopGuitar => FiveFretCoop,
                Instrument.FiveFretRhythm => FiveFretRhythm,
                Instrument.FiveFretBass => FiveFretBass,
                Instrument.Keys => Keys,
                _ => throw new ArgumentException($"Instrument {instrument} is not a 5-fret guitar instrument!")
            };
        }

        public InstrumentTrack<GuitarNote> GetSixFretTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.SixFretGuitar => SixFretGuitar,
                Instrument.SixFretCoopGuitar => SixFretCoop,
                Instrument.SixFretRhythm => SixFretRhythm,
                Instrument.SixFretBass => SixFretBass,
                _ => throw new ArgumentException($"Instrument {instrument} is not a 6-fret guitar instrument!")
            };
        }

        public InstrumentTrack<DrumNote> GetDrumsTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FourLaneDrums => FourLaneDrums,
                Instrument.ProDrums => ProDrums,
                Instrument.FiveLaneDrums => FiveLaneDrums,
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!")
            };
        }

        public InstrumentTrack<ProGuitarNote> GetProGuitarTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.ProGuitar_17Fret => ProGuitar_17Fret,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret,
                Instrument.ProBass_17Fret => ProBass_17Fret,
                Instrument.ProBass_22Fret => ProBass_22Fret,
                _ => throw new ArgumentException($"Instrument {instrument} is not a Pro Guitar instrument!")
            };
        }

        public VocalsTrack GetVocalsTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.Vocals => Vocals,
                Instrument.Harmony => Harmony,
                _ => throw new ArgumentException($"Instrument {instrument} is not a vocals instrument!")
            };
        }

        public double GetStartTime()
        {
            static double TrackMin<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Min((track) => track.GetStartTime());
            static double VoxMin(IEnumerable<VocalsTrack> tracks)
                => tracks.Min((track) => track.GetStartTime());

            double totalStartTime = 0;

            // Tracks
            totalStartTime = Math.Min(TrackMin(FiveFretTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(SixFretTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(DrumsTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(ProGuitarTracks), totalStartTime);
            totalStartTime = Math.Min(VoxMin(VocalsTracks), totalStartTime);

            // Global
            totalStartTime = Math.Min(Lyrics.GetStartTime(), totalStartTime);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalStartTime = Math.Min(GlobalEvents.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(Sections.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(SyncTrack.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(VenueTrack.GetStartTime(), totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            static double TrackMax<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Max((track) => track.GetEndTime());
            static double VoxMax(IEnumerable<VocalsTrack> tracks)
                => tracks.Max((track) => track.GetEndTime());

            double totalEndTime = 0;

            // Tracks
            totalEndTime = Math.Max(TrackMax(FiveFretTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(SixFretTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(DrumsTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(ProGuitarTracks), totalEndTime);
            totalEndTime = Math.Max(VoxMax(VocalsTracks), totalEndTime);

            // Global
            totalEndTime = Math.Max(Lyrics.GetEndTime(), totalEndTime);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalEndTime = Math.Max(GlobalEvents.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(Sections.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(SyncTrack.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(VenueTrack.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            static uint TrackMin<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Min((track) => track.GetFirstTick());
            static uint VoxMin(IEnumerable<VocalsTrack> tracks)
                => tracks.Min((track) => track.GetFirstTick());

            uint totalFirstTick = 0;

            // Tracks
            totalFirstTick = Math.Min(TrackMin(FiveFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(SixFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(DrumsTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(ProGuitarTracks), totalFirstTick);
            totalFirstTick = Math.Min(VoxMin(VocalsTracks), totalFirstTick);

            // Global
            totalFirstTick = Math.Min(Lyrics.GetFirstTick(), totalFirstTick);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalFirstTick = Math.Min(GlobalEvents.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(Sections.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(SyncTrack.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(VenueTrack.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            static uint TrackMax<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Max((track) => track.GetLastTick());
            static uint VoxMax(IEnumerable<VocalsTrack> tracks)
                => tracks.Max((track) => track.GetLastTick());

            uint totalLastTick = 0;

            // Tracks
            totalLastTick = Math.Max(TrackMax(FiveFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(SixFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(DrumsTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(ProGuitarTracks), totalLastTick);
            totalLastTick = Math.Max(VoxMax(VocalsTracks), totalLastTick);

            // Global
            totalLastTick = Math.Max(Lyrics.GetLastTick(), totalLastTick);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalLastTick = Math.Max(GlobalEvents.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(Sections.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(SyncTrack.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(VenueTrack.GetLastTick(), totalLastTick);

            return totalLastTick;
        }

        public TextEvent? GetEndEvent()
        {
            // Reverse-search through a limited amount of events
            for (int i = 1; i <= 10; i++)
            {
                int index = GlobalEvents.Count - i;
                if (index < 0)
                    break;

                var text = GlobalEvents[index];
                if (text.Text == TextEvents.END_MARKER)
                    return text;
            }

            return null;
        }
    }
}