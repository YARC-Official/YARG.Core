﻿using System;
using System.Linq;
using YARG.Core.Engine.Guitar;
using YARG.Core.Extensions;

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

            int firstRange = shifts[0].Range;

            // `+ 1` because all the lane indices in the enum are offset by one... for some reason
            Span<uint> laneEndTicks = new uint[EnumExtensions<FiveFretGuitarFret>.Count + 1];

            for (int noteIndex = 0, shiftIndex = 0; noteIndex < difficulty.Notes.Count;)
            {
                var note = difficulty.Notes[noteIndex];

                while (shiftIndex + 1 < shifts.Count && note.Time >= shifts[shiftIndex + 1].Time)
                {
                    shiftIndex++;
                }

                int shiftAmount = firstRange - shifts[shiftIndex].Range;
                if (shiftAmount > 0)
                {
                    int maxFretAllowed = (int)FiveFretGuitarFret.Orange - shiftAmount;

                    for (int j = 0; j < note.ChildNotes.Count;)
                    {
                        var child = note.ChildNotes[j];
                        if (child.Fret != (int) FiveFretGuitarFret.Open)
                        {
                            if (child.Fret > maxFretAllowed || note.Tick < laneEndTicks[child.Fret + shiftAmount])
                            {
                                note.NoteMask &= ~child.NoteMask;
                                note.DisjointMask &= ~child.DisjointMask;
                                note.ChildNotes.RemoveAt(j);
                                continue;
                            }

                            child.Fret += shiftAmount;
                            child.NoteMask <<= shiftAmount;
                            child.DisjointMask <<= shiftAmount;
                        }
                        ++j;
                    }

                    if (note.Fret != (int) FiveFretGuitarFret.Open && note.Fret > maxFretAllowed)
                    {
                        // This will automatically create a mask with all the frets pre-shifted
                        // if child notes still exist.
                        difficulty.Notes.RemoveNoteAt(noteIndex);
                        if (note.ChildNotes.Count == 0)
                        {
                            continue;
                        }
                        note = difficulty.Notes[noteIndex];
                    }
                    else
                    {
                        if (note.Fret != (int) FiveFretGuitarFret.Open)
                        {
                            note.Fret += shiftAmount;
                        }

                        if ((note.NoteMask & GuitarEngine.OPEN_MASK) != 0)
                        {
                            note.NoteMask     = ((note.NoteMask     & ~GuitarEngine.OPEN_MASK) << shiftAmount) | GuitarEngine.OPEN_MASK;
                            note.DisjointMask = ((note.DisjointMask & ~GuitarEngine.OPEN_MASK) << shiftAmount) | GuitarEngine.OPEN_MASK;
                        }
                        else
                        {
                            note.NoteMask <<= shiftAmount;
                            note.DisjointMask <<= shiftAmount;
                        }
                    }
                }
                else if (shiftAmount < 0)
                {
                    shiftAmount = -shiftAmount;
                    int minFretAllowed = (int)FiveFretGuitarFret.Green + shiftAmount;

                    for (int j = 0; j < note.ChildNotes.Count;)
                    {
                        var child = note.ChildNotes[j];
                        if (child.Fret != (int) FiveFretGuitarFret.Open)
                        {
                            if (child.Fret < minFretAllowed || note.Tick < laneEndTicks[child.Fret - shiftAmount])
                            {
                                note.NoteMask &= ~child.NoteMask;
                                note.DisjointMask &= ~child.DisjointMask;
                                note.ChildNotes.RemoveAt(j);
                                continue;
                            }

                            child.Fret -= shiftAmount;
                            child.NoteMask >>= shiftAmount;
                            child.DisjointMask >>= shiftAmount;
                        }
                        ++j;
                    }

                    if (note.Fret != (int) FiveFretGuitarFret.Open && note.Fret < minFretAllowed)
                    {
                        // This will automatically create a mask with all the frets pre-shifted
                        // if child notes still exist.
                        difficulty.Notes.RemoveNoteAt(noteIndex);
                        if (note.ChildNotes.Count == 0)
                        {
                            continue;
                        }
                        note = difficulty.Notes[noteIndex];
                    }
                    else
                    {
                        if (note.Fret != (int) FiveFretGuitarFret.Open)
                        {
                            note.Fret -= shiftAmount;
                        }

                        if ((note.NoteMask & GuitarEngine.OPEN_MASK) != 0)
                        {
                            note.NoteMask     = ((note.NoteMask     & ~GuitarEngine.OPEN_MASK) >> shiftAmount) | GuitarEngine.OPEN_MASK;
                            note.DisjointMask = ((note.DisjointMask & ~GuitarEngine.OPEN_MASK) >> shiftAmount) | GuitarEngine.OPEN_MASK;
                        }
                        else
                        {
                            note.NoteMask >>= shiftAmount;
                            note.DisjointMask >>= shiftAmount;
                        }
                    }
                }

                laneEndTicks[note.Fret] = note.Tick + note.TickLength;
                foreach (var childNote in note.ChildNotes)
                {
                    laneEndTicks[childNote.Fret] = note.Tick + childNote.TickLength;
                }
                ++noteIndex;
            }

            shifts.RemoveRange(1, shifts.Count - 1);
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