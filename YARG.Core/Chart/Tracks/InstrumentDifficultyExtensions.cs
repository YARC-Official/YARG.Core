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
            int allNoteMask = 95;

            // Maybe I'm being a bit overeager, but I'd rather not have to change this if the enum ever grows,
            // say if we had a new type of note after open.
            uint[] laneEndTicks = new uint[Enum.GetValues(typeof(FiveFretGuitarFret)).Cast<int>().Max() + 1];

            for (var i = 0; i < difficulty.Notes.Count; i++)
            {
                var note = difficulty.Notes[i];
                // Is the open bit set, regardless of any other
                bool hasOpen = (GuitarEngine.OPEN_MASK & note.NoteMask) == GuitarEngine.OPEN_MASK;
                // Is the open bit the only bit set
                bool isOpen = (~GuitarEngine.OPEN_MASK & note.NoteMask) == 0 && hasOpen;
                // Is the open bit set along with other bits
                bool isOpenChord = hasOpen && !isOpen;

                if (shiftIndex + 1 < shifts.Count && note.Time >= shifts[shiftIndex + 1].Time)
                {
                    shiftIndex++;
                    rangeFrom = shifts[shiftIndex].Range;
                    shiftAmount = rangeFrom - rangeTo;
                }

                // No shifting required if from equals to or if the only note here is an open
                if (rangeFrom == rangeTo || isOpen)
                {
                    // Store the end ticks of all the notes in the group before we continue
                    foreach (var chordNote in note.AllNotes)
                    {
                        laneEndTicks[chordNote.Fret] = chordNote.Tick + chordNote.TickLength;
                    }

                    continue;
                }

                // If this is an open chord it is possible this note is an open, so we still have to account for it
                // even though we've already determined it isn't a plain open note
                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    // Shift the note, clamping to one outside the track range
                    // This avoids accidentally shifting into an open note and makes
                    // it easier to check for invalid notes later
                    note.Fret = Math.Clamp(note.Fret - shiftAmount, 0, 6);
                }

                // Change the fret and mask according to the new range

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

                // Fix up the open bit if necessary
                if (hasOpen)
                {
                    note.NoteMask |= GuitarEngine.OPEN_MASK;
                    note.DisjointMask |= GuitarEngine.OPEN_MASK;
                }

                // Ensure there are no bits that are out of range
                note.NoteMask &= allNoteMask;
                note.DisjointMask &= allNoteMask;

                // Shift child notes
                for (int j = 0; j < note.ChildNotes.Count; j++)
                {
                    var child = note.ChildNotes[j];
                    if (child.Fret == (int) FiveFretGuitarFret.Open)
                    {
                        continue;
                    }
                    child.Fret = Math.Clamp(child.Fret - shiftAmount, 0, 6);

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

                    // Ensure there are no bits that are out of range
                    child.NoteMask &= allNoteMask;
                    child.DisjointMask &= allNoteMask;
                }

                // Check that any children are in range and remove if not
                for (int j = 0; j < note.ChildNotes.Count; j++)
                {
                    if (note.ChildNotes[j].Fret is < 1 or > 5)
                    {
                        difficulty.Notes.RemoveChildFromNote(i, j);
                        // note's referent has changed, so update note
                        note = difficulty.Notes[i];
                    }
                }

                // Check that parent is in range and remove if not
                if (note.Fret is 0 or 6)
                {
                    if (note.ChildNotes.Count == 0)
                    {
                        difficulty.Notes.RemoveNoteAt(i);
                        // All notes at this index were deleted, so start again with the same i
                        i--;
                        continue;
                    }

                    difficulty.Notes.RemoveNoteAt(i);
                    // note's referent has changed, so update note
                    note = difficulty.Notes[i];
                }

                // Check for sustain overlaps
                for (int childIndex = 0; childIndex < note.ChildNotes.Count; childIndex++)
                {
                    var child = note.ChildNotes[childIndex];
                    // Ignore opens
                    if (child.Fret == (int) FiveFretGuitarFret.Open)
                    {
                        continue;
                    }

                    if (child.Tick <= laneEndTicks[child.Fret])
                    {
                        // This child note overlaps with a sustain, so delete it
                        difficulty.Notes.RemoveChildFromNote(i, childIndex);
                    }
                }

                // Note may be stale now, so update it
                note = difficulty.Notes[i];

                // Check the parent itself for sustain overlap (opens are ignored since they don't shift)
                if (note.Fret != (int) FiveFretGuitarFret.Open && note.Tick <= laneEndTicks[note.Fret])
                {
                    if (note.ChildNotes.Count == 0)
                    {
                        difficulty.Notes.RemoveNoteAt(i);
                        // We removed the note and there are no children to be promoted, so we need to start again using the same i
                        i--;
                        continue;
                    }

                    difficulty.Notes.RemoveNoteAt(i);
                    // We removed note, so grab note again
                    note = difficulty.Notes[i];
                }

                // Store the end ticks of all the notes in the group before we continue
                foreach (var chordNote in note.AllNotes)
                {
                    laneEndTicks[chordNote.Fret] = chordNote.Tick + chordNote.TickLength;
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