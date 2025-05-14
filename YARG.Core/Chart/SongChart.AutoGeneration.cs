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
    public partial class SongChart
    {
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

        private void CreateDrumActivationPhrases()
        {
            var newActivationPhrases = new List<Phrase>();
            bool chartNeedsActivationPhrases = true;
            bool chartWasParsed = false;

            foreach (var drumTrack in new List<InstrumentTrack<DrumNote>> { ProDrums, FiveLaneDrums, FourLaneDrums })
            {
                var allPossibleDifficulties = Enum.GetValues(typeof(Difficulty));

                if (!chartWasParsed)
                {
                    // Prioritize denser charts for parsing
                    Array.Reverse(allPossibleDifficulties);
                }

                foreach (Difficulty difficultyType in allPossibleDifficulties)
                {
                    if (drumTrack.TryGetDifficulty(difficultyType, out var thisDifficultyTrack))
                    {
                        if (thisDifficultyTrack.IsEmpty)
                        {
                            // Difficulty exists but contains no data, ignore
                            continue;
                        }

                        if (!chartWasParsed)
                        {
                            // This is the first difficulty found with drum chart data
                            // Parse once and apply generated phrases to all difficulties
                            ParseForActivationPhrases(thisDifficultyTrack, newActivationPhrases);
                            chartWasParsed = true;

                            if (newActivationPhrases.Count == 0)
                            {
                                // No new activation phrases were added after parsing the chart
                                // Assume that no other difficulties will need this either
                                chartNeedsActivationPhrases = false;
                                break;
                            }
                        }

                        ApplyDrumActivationPhrases(thisDifficultyTrack, newActivationPhrases);
                    }
                }

                if (!chartNeedsActivationPhrases)
                {
                    break;
                }
            }
        }

        private void ParseForActivationPhrases(InstrumentDifficulty<DrumNote> diffChart, List<Phrase> newActivationPhrases)
        {
            var starPowerPhrases = new List<Phrase>();
            var soloPhrases = new List<Phrase>();

            foreach (var thisPhrase in diffChart.Phrases)
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
            if (starPowerPhrases.Count == 0)
            {
                YargLogger.LogDebug("Cannot generate Activation phrases for Drum chart. Not enough Star Power phrases available.");
                return;
            }

            // Limits for placing activation phrases (in seconds)
            const float MIN_SPACING_TIME = 2;
            const float MAX_SPACING_TIME = 10;

            // Update this time to the latest SP/Solo/Activation phrase encountered for comparison with the above constants
            // Start parsing after the end of the 1st SP phrase
            double spacingRefTime = starPowerPhrases[0].TimeEnd;
            int currentSPPhraseIndex = 0;

            // Align activation phrases with measure boundaries that have already been evaluated
            var measureBeatLines = SyncTrack.Beatlines.Where(x => x.Type == BeatlineType.Measure).ToList();

            int currentMeasureIndex = measureBeatLines.LowerBound(spacingRefTime);
            int totalMeasures = measureBeatLines.Count;

            // Prefer section boundaries and time signature changes for activation placement when possible
            int currentSectionIndex = Sections.LowerBound(spacingRefTime);

            var timeSigChanges = SyncTrack.TimeSignatures;
            int currentTimeSigIndex = timeSigChanges.LowerBound(spacingRefTime);

            // Do not place activation phrases inside of solo phrases
            int currentSoloIndex = soloPhrases.LowerBound(spacingRefTime);
            uint lastSoloTick = soloPhrases.GetLastTick();

            while (currentMeasureIndex < totalMeasures - 4)
            {
                // Try to move forward 4 measures
                int measuresPerActivator = 4;

                // If that is too long of a wait at the current tempo/time signature do 2 measures instead
                if (measureBeatLines[currentMeasureIndex + measuresPerActivator].Time - spacingRefTime > MAX_SPACING_TIME)
                {
                    measuresPerActivator = 2;
                }

                currentMeasureIndex += measuresPerActivator;

                var currentMeasureLine = measureBeatLines[currentMeasureIndex];

                int newSectionIndex = Sections.LowerBound(currentMeasureLine.Tick);
                if (newSectionIndex > currentSectionIndex)
                {
                    // Moved forward into a new section
                    currentSectionIndex = newSectionIndex;
                    var currentSection = Sections[currentSectionIndex];

                    //move the activation point to the start of this section
                    currentMeasureIndex = measureBeatLines.LowerBound(currentSection.Tick);
                    currentMeasureLine = measureBeatLines[currentMeasureIndex];
                }
                else
                {
                    // Still in the same section (or no sections exist), look for a time signature change
                    int newTimeSigIndex = timeSigChanges.LowerBound(currentMeasureLine.Tick);
                    if (newTimeSigIndex > currentTimeSigIndex)
                    {
                        // Moved forward into a new time signature
                        currentTimeSigIndex = newTimeSigIndex;
                        var currentTimeSig = timeSigChanges[currentTimeSigIndex];

                        //move the activation point to the start of this time signature
                        currentMeasureIndex = measureBeatLines.LowerBound(currentTimeSig.Tick);
                        currentMeasureLine = measureBeatLines[currentMeasureIndex];
                    }
                }

                uint currentMeasureTick = currentMeasureLine.Tick;

                int newSPPhraseIndex = starPowerPhrases.LowerBound(currentMeasureTick);
                if (newSPPhraseIndex > currentSPPhraseIndex)
                {
                    // New SP phrase encountered. Update reference time to the end of this SP phrase
                    // To keep the next activation phrase from appearing too close
                    currentSPPhraseIndex = newSPPhraseIndex;
                    spacingRefTime = Math.Max(starPowerPhrases[currentSPPhraseIndex].TimeEnd, spacingRefTime);
                }

                // Prevent placing an activation phrase here if it overlaps with a solo section
                if (soloPhrases.Count > 0 && currentMeasureTick < lastSoloTick)
                {
                    int newSoloIndex = soloPhrases.LowerBound(currentMeasureTick);

                    if (newSoloIndex > currentSoloIndex)
                    {
                        // Moved forward into a new solo
                        currentSoloIndex = newSoloIndex;
                        spacingRefTime = Math.Max(soloPhrases[currentSoloIndex].TimeEnd, spacingRefTime);
                    }
                }

                // This measure line is inside of or too close to an SP, solo, or activation phrase
                double currentMeasureTime = currentMeasureLine.Time;
                if (currentMeasureTime - spacingRefTime < MIN_SPACING_TIME)
                {
                    continue;
                }

                // Do not put an activation phrase here if there aren't enough notes to hit after activating SP
                const uint SP_MIN_NOTES = 16;
                int starPowerEndMeasureIndex = Math.Min(currentMeasureIndex + 4, totalMeasures - 1);
                uint starPowerEndTick = measureBeatLines[starPowerEndMeasureIndex].Tick;

                int totalNotesForStarPower = 0;
                var testNote = diffChart.Notes.UpperBoundElement(currentMeasureTick);
                while (totalNotesForStarPower < SP_MIN_NOTES && testNote != null && testNote.Tick <= starPowerEndTick)
                {
                    totalNotesForStarPower += testNote.ChildNotes.Count + 1;
                    testNote = testNote.NextNote;
                }

                if (totalNotesForStarPower < SP_MIN_NOTES)
                {
                    continue;
                }

                // This is a good place to put an Activation phrase
                spacingRefTime = currentMeasureLine.Time;

                // Mark the start of a drum fill phrase one measure before this bar line
                var previousMeasureLine = measureBeatLines[currentMeasureIndex - 1];
                double fillPhraseStartTime = previousMeasureLine.Time;
                uint fillPhraseStartTick = previousMeasureLine.Tick;

                var newDrumFillPhrase = new Phrase(
                    PhraseType.DrumFill,
                    fillPhraseStartTime,
                    currentMeasureTime - fillPhraseStartTime,
                    fillPhraseStartTick,
                    currentMeasureTick - fillPhraseStartTick
                );

                newActivationPhrases.Add(newDrumFillPhrase);
                YargLogger.LogFormatDebug("Generated a Drums SP Activation phrase from tick {0} to {1}", fillPhraseStartTick, newDrumFillPhrase.TickEnd);
            }
        }

        private void ApplyDrumActivationPhrases(InstrumentDifficulty<DrumNote> diffChart, List<Phrase> newActivationPhrases)
        {
            var allNotes = diffChart.Notes;
            uint lastNoteTick = allNotes.GetLastTick();

            foreach (var newPhrase in newActivationPhrases)
            {
                uint barLineTick = newPhrase.TickEnd;

                if (barLineTick > lastNoteTick)
                {
                    // Reached the end of this chart
                    return;
                }

                // Attempt to retrieve an activation note directly on the bar line
                var activationNote = allNotes.UpperBoundElement(barLineTick - 1);

                bool searchForAltNote = false;

                if (activationNote != null && activationNote.Tick == barLineTick)
                {
                    if (!IsIdealDrumActivationNote(activationNote, diffChart.Instrument, diffChart.Difficulty))
                    {
                        searchForAltNote = true;
                    }
                }
                else
                {
                    searchForAltNote = true;
                    activationNote = null;
                }

                if (searchForAltNote)
                {
                    // Allow a window of +/- an eighth note for syncopated activator notes
                    uint eighthNoteTickLength = newPhrase.TickLength / 8;

                    var testNote = allNotes.UpperBoundElement(barLineTick - eighthNoteTickLength - 1);
                    while (testNote != null && testNote.Tick <= barLineTick + eighthNoteTickLength)
                    {
                        if (activationNote == null)
                        {
                            activationNote = testNote;
                        }

                        if (IsIdealDrumActivationNote(testNote, diffChart.Instrument, diffChart.Difficulty))
                        {
                            activationNote = testNote;
                            break;
                        }

                        testNote = testNote.NextNote;
                    }
                }

                if (activationNote == null)
                {
                    // There are no notes in the syncopation window for this phrase
                    // Do not add to this difficulty
                    continue;
                }

                // Add the activator flag to all notes in this chord
                foreach (var note in activationNote.AllNotes)
                {
                    note.ActivateFlag(DrumNoteFlags.StarPowerActivator);
                }

                uint activationTick = activationNote.Tick;

                var phraseToApply = new Phrase(newPhrase);
                if (activationTick != barLineTick)
                {
                    // Adjust phrase length to line up with the selected activation note
                    phraseToApply.TickLength = activationTick - phraseToApply.Tick;
                    phraseToApply.TimeLength = activationNote.Time - phraseToApply.Time;
                }

                int newPhraseIndex = diffChart.Phrases.UpperBound(phraseToApply.Tick);

                if (newPhraseIndex != -1)
                {
                    // Insert new activation phrase at the appopriate index
                    diffChart.Phrases.Insert(newPhraseIndex, phraseToApply);
                }
                else
                {
                    // Add new phrase to the end of the list
                    diffChart.Phrases.Add(phraseToApply);
                }
            }
        }

        private static bool IsIdealDrumActivationNote(DrumNote note, Instrument instrument, Difficulty difficulty)
        {
            // Ignore this check on Easy/Beginner where chords are sparse
            if (difficulty < Difficulty.Medium)
            {
                return true;
            }

            bool containsCrash = false;
            bool containsKick = false;
            bool containsSnare = false;

            foreach (var childNote in note.AllNotes)
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

        // Add range shift events to the InstrumentDifficulty
        private void CreateRangeShiftPhrases()
        {
            var allPossibleDifficulties = Enum.GetValues(typeof(Difficulty));

            // Only guitar/bass have range shifts
            foreach (var track in FiveFretTracks)
            {

                foreach (Difficulty difficulty in allPossibleDifficulties)
                {
                    if (!track.TryGetDifficulty(difficulty, out var instrumentDifficulty))
                    {
                        continue;
                    }

                    if (instrumentDifficulty.Notes.Count == 0)
                    {
                        // No notes in this difficulty, nothing to do
                        continue;
                    }

                    var rangeShiftEvents = ParseRangeShifts(instrumentDifficulty);

                    for (var i = 0; i < rangeShiftEvents.Count; i++)
                    {
                        var time = rangeShiftEvents[i].Time;
                        var tick = rangeShiftEvents[i].Tick;
                        double timeEnd;
                        uint tickEnd;

                        // Overlaps will lead to bad things, so the end of one phrase is a tick before the beginning of the next
                        if (i + 1 < rangeShiftEvents.Count)
                        {
                            tickEnd = rangeShiftEvents[i + 1].Tick - 1;
                            timeEnd = SyncTrack.TickToTime(tickEnd);
                        }
                        else
                        {
                            // This is the last range shift, so it ends once all the notes are over
                            var lastNote = instrumentDifficulty.Notes[^1];
                            tickEnd = lastNote.TickEnd;
                            timeEnd = lastNote.TimeEnd;
                        }

                        var range = rangeShiftEvents[i].Range;
                        var size = rangeShiftEvents[i].Size;

                        var newPhrase = new RangeShift(time, timeEnd - time, tick, tickEnd - tick, range, size);
                        int newPhraseIndex = instrumentDifficulty.RangeShiftEvents.UpperBound(newPhrase.Tick);

                        // Add or insert the new event into the list for the given instrument difficulty
                        if (newPhraseIndex == -1)
                        {
                            instrumentDifficulty.RangeShiftEvents.Add(newPhrase);
                        }
                        else
                        {
                            instrumentDifficulty.RangeShiftEvents.Insert(newPhraseIndex, newPhrase);
                        }
                    }
                }
            }
        }

        private static List<RangeShiftEvent> ParseRangeShifts(InstrumentDifficulty<GuitarNote> instrumentDifficulty)
        {
            // List<TextEvent> textEvents = new List<TextEvent>();
            List<RangeShiftEvent> rangeShiftEvents = new List<RangeShiftEvent>();
            int size;

            foreach (var textEvent in instrumentDifficulty.TextEvents)
            {
                if (!textEvent.Text.StartsWith("ld_range_shift"))
                {
                    continue;
                }

                // Validate the event and add it to the list
                // My natural inclination here would be to use a regex, but they're not really used
                // anywhere else in YARG, so...
                var splitEvent = textEvent.Text.Split(' ');

                // 3 uses default size, 4 explicitly defines range size
                if (splitEvent.Length != 3 && splitEvent.Length != 4)
                {
                    YargLogger.LogDebug("Invalid range shift event. (Improper parameter count)");
                    continue;
                }

                if (!(int.TryParse(splitEvent[1], out int eventDifficulty) &&
                    int.TryParse(splitEvent[2], out int range)))
                {
                    YargLogger.LogDebug("Invalid range shift event. (Unable to parse difficulty and range)");
                    continue;
                }

                // Add one to eventDifficulty to make it match the Difficulty enum
                eventDifficulty++;

                if (splitEvent.Length == 4 && !int.TryParse(splitEvent[3], out size))
                {
                    YargLogger.LogDebug("Invalid range shift event. (Unable to parse size)");
                    continue;
                }
                else
                {
                    size = eventDifficulty switch
                    {
                        (int) Difficulty.Easy    => 3,
                        (int) Difficulty.Medium  => 4,
                        _                        => 5,
                    };
                }

                // Further validation
                // 1) Is the range index valid
                if (range < 1 || range > 5)
                {
                    continue;
                }

                // 2) Is the size valid for the range index
                if (range + size > 6)
                {
                    continue;
                }

                // 3) Does it apply to this difficulty?
                if ((int) instrumentDifficulty.Difficulty != eventDifficulty)
                {
                    continue;
                }

                // We have a syntactically valid range shift, so add it to the list
                rangeShiftEvents.Add(new RangeShiftEvent
                {
                    Time = textEvent.Time,
                    Tick = textEvent.Tick,
                    Range = range,
                    Size = size
                });
            }
            return rangeShiftEvents;
        }
    }

    internal struct RangeShiftEvent
    {
        public double Time;
        public uint Tick;
        public int Range;
        public int Size;
    }
}