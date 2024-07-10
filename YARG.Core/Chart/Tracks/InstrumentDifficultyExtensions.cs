using System;
using System.Collections.Generic;
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

        public static void ShuffleNotes(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            var random = new Random(133769420); // heh
            var activeNotes = new GuitarNote?[5 + 1]; // 5 frets, one open note

            foreach (var note in difficulty.Notes)
            {
                // Clear no-longer-active notes
                for (int i = 0; i < activeNotes.Length; i++)
                {
                    if (activeNotes[i] is not {} activeNote)
                        continue; // Already inactive

                    if (!IsFretActive(activeNote, note.Tick))
                        activeNotes[i] = null;
                }

                // Shuffle current chord
                foreach (var child in note.ChordEnumerator())
                {
                    // Don't shuffle open notes
                    if (child.Fret == 0)
                        continue;

                    // Check for conflicts from a prior extended sustain/disjoint chord being shuffled
                    if (activeNotes[child.Fret] != null)
                    {
                        // Find next available spot for the note
                        int nextFret = FindClosestAvailableFret(child, child.Fret);
                        if (nextFret != 0)
                        {
                            child.Fret = nextFret;
                        }
                        // If there's none, no choice but to take the L and overlap
                        else
                        {
                            YargLogger.LogFormatDebug("Unresolvable note overlap at {0:0.000} ({1}) on fret {2} (before shuffle)",
                                child.Time, child.Tick, child.Fret);
                        }
                    }

                    int newFret = GenerateFret(child);
                    if (newFret != 0)
                    {
                        // Set new fret and adjust note's active slot
                        activeNotes[child.Fret] = null;
                        activeNotes[newFret] = child;
                        child.Fret = newFret; // Must come after changing the slot
                    }
                    // Once again, no choice but to take L the if no spots are available
                    else
                    {
                        YargLogger.LogFormatDebug("Unresolvable note overlap at {0:0.000} ({1}) on fret {2} (after shuffle)",
                            child.Time, child.Tick, child.Fret);
                    }
                }
            }

            static bool IsFretActive(GuitarNote note, uint tick)
            {
                // If the note has no length, its end is inclusive
                if (note.TickLength == 0)
                    return note.TickEnd == tick;

                // Otherwise, the end is exclusive
                return note.TickEnd > tick;
            }

            // TODO: 3/4 note chords seem to have a bias towards being on the left side
            // Almost all 4-note chords get shuffled to GRYB
            int GenerateFret(GuitarNote note)
            {
                return FindClosestAvailableFret(note, note.Fret + random.Next(5) + 1);
            }

            int FindClosestAvailableFret(GuitarNote note, int targetFret)
            {
                bool rotateRight = random.Next(8) >= random.Next(8);
                for (int i = 0; i < 5; i++)
                {
                    int fret = (((targetFret - 1) + (rotateRight ? i : -i)) % 5) + 1;
                    if (activeNotes[fret] == null && (note.ParentOrSelf.NoteMask & GuitarNote.GetNoteMask(fret)) == 0)
                        return fret;
                }

                return 0;
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
    }
}