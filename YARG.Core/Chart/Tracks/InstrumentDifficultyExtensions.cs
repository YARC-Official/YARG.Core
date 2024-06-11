using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Engine.Drums;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    public static class InstrumentDifficultyExtensions
    {
        public static void ConvertToGuitarType(this InstrumentDifficulty<GuitarNote> difficulty, GuitarNoteType type)
        {
            foreach (var note in difficulty.Notes)
            {
                note.Type = type;
                foreach (var child in note.ChildNotes)
                {
                    child.Type = type;
                }
            }
        }

        public static void ConvertFromTypeToType(this InstrumentDifficulty<GuitarNote> difficulty,
            GuitarNoteType from, GuitarNoteType to)
        {
            foreach (var note in difficulty.Notes)
            {
                if (note.Type != from)
                {
                    continue;
                }

                note.Type = to;
                foreach (var child in note.ChildNotes)
                {
                    child.Type = to;
                }
            }
        }

        public static void RemoveKickDrumNotes(this InstrumentDifficulty<DrumNote> difficulty)
        {
            var kickDrumPadIndex = difficulty.Instrument switch
            {
                Instrument.ProDrums      => (int) FourLaneDrumPad.Kick,
                Instrument.FourLaneDrums => (int) FourLaneDrumPad.Kick,
                Instrument.FiveLaneDrums => (int) FiveLaneDrumPad.Kick,
                _ => throw new InvalidOperationException("Cannot remove kick drum notes from non-drum track with " +
                    $"instrument {difficulty.Instrument}!")
            };

            for (int index = 0; index < difficulty.Notes.Count; index++)
            {
                var note = difficulty.Notes[index];
                if (note.Pad != kickDrumPadIndex)
                {
                    // This is not a kick drum note, but we have to check it's children too
                    int? childNoteKickIndex = null;
                    for (int i = 0; i < note.ChildNotes.Count; i++)
                    {
                        var childNote = note.ChildNotes[i];
                        if (childNote.Pad == kickDrumPadIndex)
                        {
                            childNoteKickIndex = i;
                            break;
                        }
                    }

                    if (childNoteKickIndex != null)
                    {
                        var newNote = note.CloneWithoutChildNotes();
                        for (int i = 0; i < note.ChildNotes.Count; i++)
                        {
                            if (i != childNoteKickIndex)
                            {
                                newNote.AddChildNote(note.ChildNotes[i]);
                            }
                        }

                        difficulty.Notes[index] = newNote;
                    }
                }
                else if (note.ChildNotes.Count > 0)
                {
                    // If the drum note has child notes, convert the first child note to a parent note,
                    // then assign the other child notes to this parent note.
                    // Finally, overwrite the drum note with the new parent note.
                    var firstChild = note.ChildNotes[0].CloneWithoutChildNotes();
                    for (int i = 1; i < note.ChildNotes.Count; i++)
                    {
                        firstChild.AddChildNote(note.ChildNotes[i]);
                    }

                    difficulty.Notes[index] = firstChild;
                }
                else
                {
                    // This is a single kick drum note
                    difficulty.Notes.RemoveAt(index);

                    if (note.IsStarPowerActivator)
                    {
                        // This is a single kick drum note that is a star power activator,
                        // we have to move it to the NEXT note.
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].DrumFlags |= DrumNoteFlags.StarPowerActivator;
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.DrumFlags |= DrumNoteFlags.StarPowerActivator;
                            }
                        }
                    }

                    if (note.IsSoloStart && !note.IsSoloEnd)
                    {
                        // This is a single kick drum note that is a solo start, we have to move it to the
                        // NEXT note (we don't want to extend the solo).
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].Flags |= NoteFlags.SoloStart;
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.Flags |= NoteFlags.SoloStart;
                            }
                        }
                    }

                    if (note.IsSoloEnd)
                    {
                        // This is a single kick drum note that is a solo end, we have to move it to the
                        // PREVIOUS note (we don't want to extend the solo).
                        if (index > 0)
                        {
                            difficulty.Notes[index - 1].Flags |= NoteFlags.SoloEnd;
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index - 1].ChildNotes)
                            {
                                childNote.Flags |= NoteFlags.SoloEnd;
                            }
                        }
                    }

                    if (note.IsStarPowerStart && !note.IsStarPowerEnd)
                    {
                        // This is a single kick drum note that is a starpower start, we have to move it to the
                        // NEXT note (we don't want to extend the starpower section).
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].Flags |= NoteFlags.StarPowerStart;
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.Flags |= NoteFlags.StarPowerStart;
                            }
                        }
                    }

                    if (note.IsStarPowerEnd)
                    {
                        // This is a single kick drum note that is a starpower end, we have to move it to the
                        // PREVIOUS note (we don't want to extend the starpower section).
                        if (index > 0)
                        {
                            difficulty.Notes[index - 1].Flags |= NoteFlags.StarPowerEnd;
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index - 1].ChildNotes)
                            {
                                childNote.Flags |= NoteFlags.StarPowerEnd;
                            }
                        }
                    }

                    index--;
                }

                // Since we modified and/or removed notes, we have to map the previous notes correctly again
                if (index >= 0)
                {
                    if (index > 1)
                    {
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index - 1].NextNote = difficulty.Notes[index];
                        }
                        else
                        {
                            difficulty.Notes[index - 1].NextNote = null;
                        }
                    }

                    if (index > 0)
                    {
                        difficulty.Notes[index].PreviousNote = difficulty.Notes[index - 1];
                    }
                    else
                    {
                        difficulty.Notes[index].PreviousNote = null;
                    }
                }
            }
        }

        public static void GenerateActivationPhrases(this InstrumentDifficulty<DrumNote> difficulty, SyncTrack syncTrack)
        {
            if (difficulty.Notes.Count == 0) return;

            uint lastNoteTick = difficulty.Notes.GetLastTick();

            // Automatically add in Star Power activation phrases for drum charts that don't have enough

            List<Phrase> starPowerPhrases = new();
            List<Phrase> soloPhrases = new();

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
            var measureBeatLines = syncTrack.Beatlines.Where(x => x.Type == BeatlineType.Measure).ToList();

            int currentMeasureIndex = measureBeatLines.GetIndexOfPrevious(prevReferenceTime);
            if (currentMeasureIndex == -1) return;

            int totalMeasures = measureBeatLines.Count;
            
            var allTimeSigs = syncTrack.TimeSignatures;
            var currentTimeSigIndex = allTimeSigs.GetIndexOfPrevious(prevReferenceTime);

            if (currentTimeSigIndex == -1) return;
            
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

                int newTimeSigIndex = syncTrack.TimeSignatures.GetIndexOfPrevious(currentMeasureLine.Tick);
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

                uint eighth_note_tick_length = currentTimeSig.GetTicksPerBeat(syncTrack) / 2;
                
                // Attempt to retrieve an activation note directly on the bar line
                var activationNote = difficulty.Notes.GetPrevious(currentMeasureTick);

                // No more notes in this chart
                if (activationNote == null) break;

                // No note exists on or near the bar line, move on
                if (activationNote.Tick < currentMeasureTick - eighth_note_tick_length) continue;

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
                int starPowerEndMeasureIndex = Math.Min(currentMeasureIndex+4, totalMeasures-1);
                uint starPowerEndTick = measureBeatLines[starPowerEndMeasureIndex].Tick;

                int totalNotesForStarPower = 0;
                var testNote = activationNote.NextNote;
                while (totalNotesForStarPower < SP_MIN_NOTES && testNote != null)
                {
                    if (testNote.Tick > starPowerEndTick) break;
                    
                    totalNotesForStarPower += testNote.ChildNotes.Count+1;
                    testNote = testNote.NextNote;
                }

                if (totalNotesForStarPower < SP_MIN_NOTES) continue;

                // This is a good place to put an Activation note
                prevReferenceTime = currentMeasureLine.Time;

                if (!DrumsEngine.IsIdealActivationNote(activationNote, difficulty.Instrument))
                {
                    // The note on the bar line doesn't contain a combo of crash and kick/snare
                    // Search +/- an 8th note to find a possible syncopated activation note
                    testNote = difficulty.Notes.GetNext(currentMeasureTick - eighth_note_tick_length - 1);
                    while (testNote != null && testNote.Tick <= currentMeasureTick + eighth_note_tick_length)
                    {
                        if (testNote != activationNote)
                        {
                            if (DrumsEngine.IsIdealActivationNote(testNote, difficulty.Instrument))
                            {
                                activationNote = testNote;
                                break;
                            }
                        }

                        testNote = testNote.NextNote;
                    }
                }

                // Add the activator flag to all notes in this chord
                foreach (var note in activationNote.AllNotes)
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
    }
}