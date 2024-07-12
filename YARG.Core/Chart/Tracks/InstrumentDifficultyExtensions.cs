using System;
using System.Collections.Generic;
using YARG.Core.Extensions;
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

        public static void ShuffleNotes(this InstrumentDifficulty<GuitarNote> difficulty, int seed)
        {
            var random = new Random(seed);
            var activeNotes = new GuitarNote?[5 + 1]; // 5 frets, one open note

            var currentNotes = new List<GuitarNote>();
            var availableFrets = new List<int>();
            var selectedFrets = new List<int>();

            foreach (var parent in difficulty.Notes)
            {
                // Reset state
                // Must be done first and not last, otherwise skipping a note won't reset state
                currentNotes.Clear();
                selectedFrets.Clear();
                availableFrets.Clear();

                // Clear no-longer-active notes
                for (int i = 0; i < activeNotes.Length; i++)
                {
                    if (activeNotes[i] is not {} activeNote)
                        continue; // Already inactive

                    bool isActive = activeNote.TickLength == 0
                        ? activeNote.TickEnd == parent.Tick // If the note has no length, its end is inclusive
                        : activeNote.TickEnd > parent.Tick; // Otherwise, the end is exclusive

                    if (!isActive)
                        activeNotes[i] = null;
                }

                // Set up grab bag
                if (activeNotes[1] == null) availableFrets.Add(1);
                if (activeNotes[2] == null) availableFrets.Add(2);
                if (activeNotes[3] == null) availableFrets.Add(3);
                if (activeNotes[4] == null) availableFrets.Add(4);
                if (activeNotes[5] == null) availableFrets.Add(5);
                availableFrets.Shuffle(random);

                // Pick a set of random notes for the chord
                foreach (var note in parent.ChordEnumerator())
                {
                    // Don't shuffle open notes
                    if (note.Fret == 0)
                        continue;

                    if (availableFrets.Count < 1)
                    {
                        // Ignore un-shuffleable notes
                        YargLogger.LogFormatWarning("Cannot shuffle note at {0:0.000} ({1}), removing.", note.Time, note.Tick);
                        continue;
                    }

                    int randomFret = availableFrets.PopRandom(random);
                    currentNotes.Add(note);
                    selectedFrets.Add(randomFret);
                }

                // Remove any notes that didn't make the cut
                for (int i = 0; i < parent.ChildNotes.Count; i++)
                {
                    var child = parent.ChildNotes[i];
                    if (!currentNotes.Contains(child) && child.Fret != 0) // Don't remove open notes
                        parent.RemoveChildNote(child);
                }

                // Skip open notes and 5-note chords
                if (currentNotes.Count < 1 || currentNotes.Count >= 5)
                    continue;

                // Sort notes/frets to prepare for the next step
                currentNotes.Sort((left, right) => left.Fret.CompareTo(right.Fret));
                selectedFrets.Sort();

                // Push all notes to the right, to prevent intermediate overlaps
                for (int i = 0; i < currentNotes.Count; i++)
                {
                    currentNotes[^(i + 1)].Fret = 5 - i;
                }

                // Apply shuffled frets
                YargLogger.Assert(currentNotes.Count == selectedFrets.Count);
                for (int i = 0; i < selectedFrets.Count; i++)
                {
                    int fret = selectedFrets[i];
                    var note = currentNotes[i];

                    note.Fret = fret;
                    activeNotes[fret] = note;
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
    }
}