using System;
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

            // Automatically add in Star Power activation phrases for charts that don't have any
            var DrumSPActivationPhrases = difficulty.Phrases.Where(thisPhrase => thisPhrase.Type == PhraseType.DrumFill).ToList();
            
            // Assumes that any chart that contains manually authored activation phrases will have enough
            if (DrumSPActivationPhrases.Count > 0) return;

            // Activation cannot occur before the player has enough SP to activate
            // Start processing after the 2nd SP phrase
            var starPowerPhrases = difficulty.Phrases.Where(thisPhrase => thisPhrase.Type == PhraseType.StarPower).ToList();
            
            if (starPowerPhrases.Count < 2)
            {
                YargLogger.LogDebug("Cannot generate Activation phrases for Drum chart. Not enough Star Power phrases available.");
                return;
            }

            uint currentTick = starPowerPhrases[1].TickEnd;
            double prevReferenceTime = starPowerPhrases[1].TimeEnd;

            uint lastTick = difficulty.Notes.GetLastTick();

            var currentTimeSig = syncTrack.TimeSignatures.GetPrevious(currentTick);

            if (currentTimeSig == null) return;

            uint currentTicksPerMeasure = currentTimeSig.GetTicksPerMeasure(syncTrack);

            // Limits for placing activation phrases (in seconds)
            const float MIN_SPACING = 3;
            const float MAX_SPACING = 10;

            do
            {
                uint ticksToAdd = currentTicksPerMeasure * 4;
                while (syncTrack.TickToTime(currentTick + ticksToAdd) - prevReferenceTime > MAX_SPACING)
                {
                    ticksToAdd /= 2;

                    if (ticksToAdd < currentTicksPerMeasure)
                    {
                        // Slow tempo and time signature combination, only wait a single measure before adding a new activation phrase
                        ticksToAdd = currentTicksPerMeasure;
                        break;
                    }
                }

                // TimeSignatureChange.TickLength is not set, use the Tick value of the next time signature instead
                var nextTimeSig = syncTrack.TimeSignatures.GetNext(currentTick+1);
                uint currentTimeSigTickEnd = nextTimeSig == null ? lastTick : nextTimeSig.Tick;

                // If the time signature changes before the next 4 measures have passed
                // Put the activation note at the start of the new time signature instead
                currentTick = Math.Min(currentTick + ticksToAdd, currentTimeSigTickEnd);
                
                // Attempt to retrieve an activation note directly on the bar line
                var activationNote = difficulty.Notes.GetPrevious(currentTick);

                // No more notes in this chart
                if (activationNote == null) break;

                if (activationNote.Tick < currentTick)
                {
                    // No note exists on the bar line, attempt to retrieve the next note instead
                    activationNote = activationNote.NextNote;

                    // No more notes in this chart
                    if (activationNote == null) break;
                }

                currentTick = activationNote.Tick;

                if (currentTimeSigTickEnd <= currentTick)
                {
                    currentTimeSig = nextTimeSig;
                    if (currentTimeSig == null) break;

                    currentTicksPerMeasure = currentTimeSig.GetTicksPerMeasure(syncTrack);
                }

                if (syncTrack.TickToTime(currentTick) - prevReferenceTime < MIN_SPACING) continue;

                var nearestSPPhrase = starPowerPhrases.GetPrevious(currentTick);
                if (nearestSPPhrase == null) break;

                if (nearestSPPhrase.TimeEnd > prevReferenceTime)
                {
                    // Prevent activation phrases from appearing less than MIN_SPACING away from SP phrases
                    prevReferenceTime = nearestSPPhrase.TimeEnd;
                }

                // Do not put an activation phrase here if it overlaps with an existing SP phrase
                if (nearestSPPhrase.TickEnd > currentTick) continue;
                
                // Do not put an activation phrase here if there aren't enough notes to hit after activating SP
                // Assumes no time signature changes for the next 4 measures
                const uint SP_MIN_NOTES = 10;
                uint starPowerEndTick = currentTick + (currentTicksPerMeasure * 4);

                int totalNotesForStarPower = 0;
                var testNote = activationNote.NextNote;
                while (totalNotesForStarPower < SP_MIN_NOTES && testNote != null)
                {
                    if (testNote.Tick > starPowerEndTick) break;
                    
                    totalNotesForStarPower += testNote.ChildNotes.Count;
                    testNote = testNote.NextNote;
                }

                if (totalNotesForStarPower < SP_MIN_NOTES) continue;

                // This is a good place to put an Activation note
                prevReferenceTime = syncTrack.TickToTime(currentTick);

                if (!DrumsEngine.IsIdealActivationNote(activationNote, difficulty.Instrument))
                {
                    // The note on the bar line doesn't contain a combo of crash and kick/snare
                    // Search +/- an 8th note to find a possible syncopated activation note
                    uint eighth_note_length = currentTimeSig.GetTicksPerBeat(syncTrack) / 2;

                    testNote = difficulty.Notes.GetNext(currentTick - eighth_note_length);
                    while (testNote != null && testNote.Tick <= currentTick + eighth_note_length)
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
                uint fillPhraseStartTick = currentTick - (ticksToAdd / 4);
                uint fillPhraseEndTick = activationNote.Tick;
                double fillPhraseStartTime = syncTrack.TickToTime(fillPhraseStartTick);
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
            } while (currentTick < lastTick);
        }
    }
}