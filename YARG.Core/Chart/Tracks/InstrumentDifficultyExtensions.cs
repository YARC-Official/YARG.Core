using System;
using System.Linq;
using YARG.Core.Engine.Guitar;

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

        // Transposes all ranges into the first range.
        // For example, if the song starts in the GRY range and later shifts to the RYB or YBO ranges
        // the notes in the later ranges are transposed into the first range. (If there was a case where the
        // original range was GRY and a subsequent range was RYBO, which shouldn't actually happen, RYBO would
        // be transposed into GRYB)
        public static void CompressGuitarRange(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            // Bail if there aren't actually any range shift events
            if (difficulty.RangeShiftEvents.Count == 0)
            {
                return;
            }

            var shifts = difficulty.RangeShiftEvents;

            // We're shifting all ranges into the first range, so set rangeTo to the first range
            int rangeTo = shifts[0].Range;
            // These need to be initialized here since the first range won't have set them in the loop
            int rangeFrom = shifts[0].Range;
            int shiftIndex = 0;
            int shiftAmount = rangeFrom - rangeTo;

            for (var i = 0; i < difficulty.Notes.Count; i++)
            {
                var note = difficulty.Notes[i];
                bool isOpen = (GuitarEngine.OPEN_MASK & note.NoteMask) == GuitarEngine.OPEN_MASK;;
                bool isOpenChord = (GuitarEngine.OPEN_MASK & note.NoteMask) > GuitarEngine.OPEN_MASK;
                int openBit = GuitarEngine.OPEN_MASK & note.NoteMask;

                if (shiftIndex + 1 < shifts.Count && note.Time >= shifts[shiftIndex + 1].Time)
                {
                    shiftIndex++;
                    rangeFrom = shifts[shiftIndex].Range;
                    shiftAmount = rangeFrom - rangeTo;
                }

                // No shifting required if from equals to or if the only note here is an open
                if (rangeFrom == rangeTo || isOpen)
                {
                    continue;
                }

                // Change the fret and mask according to the new range

                // If this is an open chord it is possible this note is an open, so we still have to account for it
                // even though we've already determined it isn't a plain open note
                note.Fret -= note.Fret == (int) FiveFretGuitarFret.Open ? 0 : shiftAmount;

                // Remove any open note from the masks
                note.NoteMask &= ~GuitarEngine.OPEN_MASK;
                note.DisjointMask &= ~GuitarEngine.OPEN_MASK;

                if (shiftAmount > 0)
                {
                    note.NoteMask >>= shiftAmount;
                    note.DisjointMask >>= shiftAmount;
                }
                else
                {
                    note.NoteMask <<= -shiftAmount;
                    note.DisjointMask <<= -shiftAmount;
                }

                // Fix up the open bit by clearing the open bit and then ORing with openBit, in case it was munged
                // by a left shift
                note.NoteMask = (note.NoteMask & ~GuitarEngine.OPEN_MASK) | openBit;
                note.DisjointMask = (note.DisjointMask & ~GuitarEngine.OPEN_MASK) | openBit;

                // Shift child notes
                for (int j = 0; j < note.ChildNotes.Count; j++)
                {
                    var child = note.ChildNotes[j];
                    child.Fret -= child.Fret == (int) FiveFretGuitarFret.Open ? 0 : shiftAmount;

                    // Children that aren't themselves an open are guaranteed not to have the open bit set
                    // so we can just shift without having to worry about fixing the open bit since a child
                    // that is an open will shift by zero

                    if (shiftAmount > 0)
                    {
                        child.NoteMask >>= shiftAmount;
                        child.DisjointMask >>= shiftAmount;
                    }
                    else
                    {
                        child.NoteMask <<= -shiftAmount;
                        child.DisjointMask <<= -shiftAmount;
                    }
                }

                // Check for validity of (possible) parent and remove if off track, reparenting children as necessary
                var count = note.ChildNotes.Count + 1;
                bool outOfRange = false;

                foreach (var chordNote in note.AllNotes)
                {
                    if (chordNote.Fret is < (int) FiveFretGuitarFret.Green or > (int) FiveFretGuitarFret.Orange)
                    {
                        outOfRange = true;
                    }
                }

                if (!outOfRange)
                {
                    continue;
                }

                // Check if parent is out of range, replacing parent with a child if so, and keep doing
                // that until we get a parent that is in range or we run out of children
                while (note.Fret is < (int) FiveFretGuitarFret.Green or > (int) FiveFretGuitarFret.Orange)
                {
                    if (count == 0)
                    {
                        // Parent and all children have been removed, so we're done
                        break;
                    }

                    difficulty.Notes.RemoveNoteAt(i);

                    note = difficulty.Notes[i];
                    count--;
                }

                if (count == 0)
                {
                    // We ended up deleting all the notes, so we need to start again using the same i
                    i--;
                    continue;
                }

                // Now check that any remaining children are in range and remove if not
                // It should be noted that when shifting down (towards green), this is the part
                // that really does all the work since the parent is actually the high note of the chord
                for (int j = 0; j < note.ChildNotes.Count; j++)
                {
                    if (note.ChildNotes[j].Fret is < 1 or > 5)
                    {
                        difficulty.Notes.RemoveChildFromNote(i, j);
                        // note's referent has changed, so update note
                        note = difficulty.Notes[i];
                    }
                }

                // If we're here, we are working with a broken chart, so we should check for overlapping sustains

                // There is no previous note, so an overlapping sustain isn't possible
                if (i == 0)
                {
                    continue;
                }

                // Now that we know what the final note (group) for this index is, make sure any previous sustain
                // doesn't overlap with the current note

                // I guess that we really need to search for the previous note in any of this note group's
                // lanes, not just look back a single index. We only need to go as far as is required to
                // find the previous note in any of this group's lanes.

                // GRYBO-Open
                int[] overlapIndexes = { -1, -1, -1, -1, -1, -1 };

                for (int n = i; n >= 0; n--)
                {
                    if ((note.NoteMask & difficulty.Notes[n].NoteMask) != 0)
                    {
                        // Figure out which notes overlap
                        for (int m = 1; m < 7; m++)
                        {
                            if ((note.NoteMask & difficulty.Notes[n].NoteMask & (1 << m)) != 0 && overlapIndexes[m - 1] == -1)
                            {
                                overlapIndexes[m - 1] = n;
                            }
                        }

                        // If overlapIndexes contains as many valid entries as there are notes in our group, we're done
                        if (overlapIndexes.Count(x => x != -1) == note.ChildNotes.Count + 1)
                        {
                            break;
                        }
                    }
                }

                // For each note in our note group, check to see if the identified previous note is a sustain
                foreach (var parentOrChild in note.AllNotes)
                {
                    if (overlapIndexes[parentOrChild.Fret - 1] == -1)
                    {
                        continue;
                    }

                    foreach (var prevNote in difficulty.Notes[overlapIndexes[parentOrChild.Fret - 1]].AllNotes)
                    {
                        // If it isn't on the same fret or it isn't a sustain, we don't care about it
                        if (prevNote.Fret != parentOrChild.Fret || !prevNote.IsSustain)
                        {
                            continue;
                        }

                        // We have found a sustain on the same fret as one of our notes, so we have to cut it off
                        // at note.Tick
                        if (prevNote.Tick + prevNote.TickLength >= parentOrChild.Tick)
                        {
                            prevNote.TickLength = parentOrChild.Tick - prevNote.Tick;
                        }
                    }
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

        public static void RemoveDynamics(this InstrumentDifficulty<DrumNote> difficulty)
        {
            foreach (var i in difficulty.Notes)
            {
                foreach (var note in i.AllNotes)
                {
                    note.Type = DrumNoteType.Neutral;
                }
            }
        }
    }
}