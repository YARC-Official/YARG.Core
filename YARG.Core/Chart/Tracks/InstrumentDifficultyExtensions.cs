using System;
using System.Collections.Generic;
using YARG.Core.Engine.Guitar;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class InstrumentDifficultyExtensions
    {
        /// <summary>
        /// Converts a 5-fret guitar difficulty into a 6-fret copy suitable for six-fret gameplay.
        ///
        /// The conversion is a direct per-chord lookup: 5-fret and 6-fret fret values coincide
        /// numerically (Green-Orange = Black1-White2, Open is shared), so chords map 1:1 except
        /// for a fixed table of chord substitutions (see Docs/5fret_to_6fret_conversion.md).
        /// The full GRYBO chord maps to holding all six frets.
        ///
        /// When a chord strikes into a column that already carries an active sustain, the earlier
        /// sustain is truncated at that tick (real charters cut sustains when a pattern needs
        /// the space). See Docs/5fret_to_6fret_conversion.md for the full description.
        /// </summary>
        public static InstrumentDifficulty<GuitarNote> ConvertFiveFretToSixFret(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            var converted = new InstrumentDifficulty<GuitarNote>(difficulty);

            var activeSustains = new List<(uint TickEnd, int Fret, GuitarNote Source)>();
            foreach (var note in converted.Notes)
            {
                // Drop sustains that ended before this chord
                activeSustains.RemoveAll(s => s.TickEnd <= note.Tick);

                // Collect fretted members; Open/Wildcard members keep their shared value (7/8)
                int frettedMask = 0;
                foreach (var member in note.AllNotes)
                {
                    if (member.Fret is (int) FiveFretGuitarFret.Open or (int) FiveFretGuitarFret.Wildcard)
                    {
                        continue;
                    }

                    frettedMask |= 1 << (member.Fret - 1);
                }

                if (frettedMask == 0)
                {
                    continue;
                }

                FiveFretToSixFretSubstitutions.TryGetValue(frettedMask, out var substitution);

                // Apply the direct mapping: identity unless this chord shape has a substitution
                var placedMembers = new List<(GuitarNote Note, int Fret)>();
                int placedMask = 0;
                int placedColumns = 0;
                foreach (var member in note.AllNotes)
                {
                    if (member.Fret is (int) FiveFretGuitarFret.Open or (int) FiveFretGuitarFret.Wildcard)
                    {
                        continue;
                    }

                    int placedFret = substitution is null ? member.Fret : substitution[member.Fret];
                    SetSixFret(member, placedFret);
                    placedMask |= 1 << (placedFret - 1);
                    placedColumns |= 1 << ColumnOf(placedFret);
                    placedMembers.Add((member, placedFret));
                }

                // The full GRYBO chord maps to holding all six frets: add a real sixth member
                // on the remaining pad so the chord is complete both visually and mechanically.
                // Added after the mapping so its fret (White3) cannot collide with a member.
                if (frettedMask == FIVE_FRET_GRYBO_MASK)
                {
                    var sixthFret = new GuitarNote((int) SixFretGuitarFret.White3, note.Type,
                        GuitarNoteFlags.None, NoteFlags.None, note.Time, 0, note.Tick, 0);
                    note.AddChildNote(sixthFret);
                    placedMask |= 1 << ((int) SixFretGuitarFret.White3 - 1);
                    placedColumns |= 1 << ColumnOf((int) SixFretGuitarFret.White3);
                }

                // Truncate any active sustains this chord strikes into — a new sustain in a
                // column replaces the older one rather than stacking onto it
                for (int i = activeSustains.Count - 1; i >= 0; i--)
                {
                    var (_, fret, source) = activeSustains[i];
                    if ((placedColumns & (1 << ColumnOf(fret))) == 0)
                    {
                        continue;
                    }

                    TruncateSustain(source, note.Tick);
                    activeSustains.RemoveAt(i);
                }

                // Track this chord's sustains so later chords can truncate them
                foreach (var (member, placedFret) in placedMembers)
                {
                    if (member.TickLength > 0)
                    {
                        activeSustains.Add((member.Tick + member.TickLength, placedFret, member));
                    }
                }

                // Parent NoteMask: replace the fretted bits with the placed ones, keep open-like bits
                int openBits = note.NoteMask & ~SIX_FRET_FRETS_MASK;
                note.NoteMask = openBits | placedMask;
            }
            return converted;
        }

        /// <summary>
        /// Returns a copy of a 6-fret difficulty with every note's pads color-flipped
        /// (Black1↔White1, Black2↔White2, Black3↔White3). Used for lefty flip: the mirrored
        /// highway physically swaps the two pad rows, so notes must swap rows with it.
        /// Open and wildcard notes are unchanged.
        /// </summary>
        public static InstrumentDifficulty<GuitarNote> FlipSixFretColors(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            var flipped = new InstrumentDifficulty<GuitarNote>(difficulty);
            foreach (var note in flipped.Notes)
            {
                int memberMaskUnion = 0;
                foreach (var member in note.AllNotes)
                {
                    if (member.Fret is >= (int) SixFretGuitarFret.Black1 and <= (int) SixFretGuitarFret.White3)
                    {
                        member.Fret = (member.Fret - 1 + 3) % 6 + 1;
                    }

                    member.NoteMask = FlipSixFretColorBits(member.NoteMask);
                    member.DisjointMask = FlipSixFretColorBits(member.DisjointMask);
                    memberMaskUnion |= member.NoteMask;
                }

                // The parent's NoteMask is the union of its members' masks
                note.NoteMask = memberMaskUnion;
            }
            return flipped;
        }

        /// <summary>Swaps the black-row bits (1-3) with the white-row bits (4-6), keeping open/wildcard bits.</summary>
        private static int FlipSixFretColorBits(int mask)
        {
            return ((mask & 0x38) >> 3) | ((mask & 0x07) << 3) | (mask & ~SIX_FRET_FRETS_MASK);
        }

        /// <summary>Masks for the six fretted 6-fret notes (Black1 through White3).</summary>
        private const int SIX_FRET_FRETS_MASK = 0x3F;

        /// <summary>Mask of all five fretted 5-fret notes (Green through Orange).</summary>
        private const int FIVE_FRET_GRYBO_MASK = 0b11111;

        /// <summary>
        /// Substitutions for chord shapes whose direct mapping is undesirable, keyed by the
        /// chord's 5-fret fret mask. Each entry maps 5-fret fret values to 6-fret fret values;
        /// chords not listed here map 1:1. See Docs/5fret_to_6fret_conversion.md.
        /// </summary>
        private static readonly Dictionary<int, int[]> FiveFretToSixFretSubstitutions = new()
        {
            [ChordMask(FiveFretGuitarFret.Green, FiveFretGuitarFret.Red, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Green, SixFretGuitarFret.Black1),
                        (FiveFretGuitarFret.Red, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.White3)),

            [ChordMask(FiveFretGuitarFret.Red, FiveFretGuitarFret.Blue, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Red, SixFretGuitarFret.Black2),
                        (FiveFretGuitarFret.Blue, SixFretGuitarFret.White2),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.White3)),

            [ChordMask(FiveFretGuitarFret.Yellow, FiveFretGuitarFret.Blue, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Yellow, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Blue, SixFretGuitarFret.White2),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.Black3)),

            [ChordMask(FiveFretGuitarFret.Green, FiveFretGuitarFret.Red, FiveFretGuitarFret.Yellow, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Green, SixFretGuitarFret.Black1),
                        (FiveFretGuitarFret.Red, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Yellow, SixFretGuitarFret.Black2),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.White3)),

            [ChordMask(FiveFretGuitarFret.Green, FiveFretGuitarFret.Red, FiveFretGuitarFret.Blue, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Green, SixFretGuitarFret.Black1),
                        (FiveFretGuitarFret.Red, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Blue, SixFretGuitarFret.Black3),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.White3)),

            [ChordMask(FiveFretGuitarFret.Red, FiveFretGuitarFret.Yellow, FiveFretGuitarFret.Blue, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Red, SixFretGuitarFret.Black1),
                        (FiveFretGuitarFret.Yellow, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Blue, SixFretGuitarFret.White2),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.White3)),

            [ChordMask(FiveFretGuitarFret.Green, FiveFretGuitarFret.Red, FiveFretGuitarFret.Yellow, FiveFretGuitarFret.Blue, FiveFretGuitarFret.Orange)] =
                FretMap((FiveFretGuitarFret.Green, SixFretGuitarFret.Black1),
                        (FiveFretGuitarFret.Red, SixFretGuitarFret.White1),
                        (FiveFretGuitarFret.Yellow, SixFretGuitarFret.Black2),
                        (FiveFretGuitarFret.Blue, SixFretGuitarFret.White2),
                        (FiveFretGuitarFret.Orange, SixFretGuitarFret.Black3)),
        };

        private static int ChordMask(params FiveFretGuitarFret[] frets)
        {
            int mask = 0;
            foreach (var fret in frets)
            {
                mask |= 1 << ((int) fret - 1);
            }
            return mask;
        }

        private static int[] FretMap(params (FiveFretGuitarFret From, SixFretGuitarFret To)[] pairs)
        {
            var map = new int[(int) FiveFretGuitarFret.Orange + 1];
            foreach (var (from, to) in pairs)
            {
                map[(int) from] = (int) to;
            }
            return map;
        }

        /// <summary>Column index (0-2) of a fretted 6-fret value: Black1/White1 = 0, etc.</summary>
        private static int ColumnOf(int fret)
        {
            return (fret - 1) % 3;
        }

        private static void TruncateSustain(GuitarNote note, uint tick)
        {
            if (tick <= note.Tick)
            {
                note.TickLength = 0;
                note.TimeLength = 0;
                return;
            }

            uint oldTickLength = note.TickLength;
            note.TickLength = tick - note.Tick;
            if (oldTickLength > 0)
            {
                note.TimeLength *= (double) note.TickLength / oldTickLength;
            }
        }

        private static void SetSixFret(GuitarNote member, int fret)
        {
            member.Fret = fret;
            int mask = 1 << (fret - 1);
            member.DisjointMask = (member.DisjointMask & ~SIX_FRET_FRETS_MASK) | mask;
            if (member.IsChild)
            {
                member.NoteMask = (member.NoteMask & ~SIX_FRET_FRETS_MASK) | mask;
            }
        }

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

        public static void ConvertFromOpenToGreen(this InstrumentDifficulty<GuitarNote> difficulty, SyncTrack syncTrack)
        {
            GuitarNote? lastGreenSustain = null;
            GuitarNote? currentGreen = null;
            GuitarNote? currentOpen = null;
            bool openInLastGreenSustainChordBeforeConversion = false;
            int lastNoteMask = 0;
            uint sixteenthTickLength = syncTrack.Resolution / 4;
            int noteMaskGreen = 1 << (FiveFretGuitarFret.Green.Convert() - 1);
            int noteMaskOpen = 1 << (FiveFretGuitarFret.Open.Convert() - 1);
            foreach (var note in difficulty.Notes)
            {
                if (note.Fret == FiveFretGuitarFret.Open.Convert())
                {
                    currentOpen = note;
                }
                if (note.Fret == FiveFretGuitarFret.Green.Convert())
                {
                    currentGreen = note;
                }

                if (note.IsParent)
                {
                    foreach (var childNote in note.ChildNotes)
                    {
                        if (childNote.Fret == FiveFretGuitarFret.Open.Convert())
                        {
                            currentOpen = childNote;
                        }
                        if (childNote.Fret == FiveFretGuitarFret.Green.Convert())
                        {
                            currentGreen = childNote;
                        }
                    }
                }

                if (currentOpen != null || currentGreen != null)
                {
                    //cut off last green sustain early if there is another green note on it
                    if (lastGreenSustain != null && (currentOpen != null || openInLastGreenSustainChordBeforeConversion) &&
                        lastGreenSustain.Tick + lastGreenSustain.TickLength + sixteenthTickLength > note.Tick)
                    {
                        //if sustain would be cut off before starting remove sustain
                        if (note.Tick - lastGreenSustain.Tick <= sixteenthTickLength)
                        {
                            lastGreenSustain.TickLength = 0;
                            lastGreenSustain.TimeLength = 0;
                        }
                        else
                        {
                            lastGreenSustain.TickLength = note.Tick - sixteenthTickLength - lastGreenSustain.Tick;
                            lastGreenSustain.TimeLength = syncTrack.TickToTime(note.Tick - sixteenthTickLength) -
                                lastGreenSustain.Time;
                        }
                    }
                }

                //P note without G
                if (currentGreen == null && currentOpen != null)
                {
                    currentOpen.Fret = FiveFretGuitarFret.Green.Convert();
                    //or the mask with the mask for green and then And the mask with all bits except purple
                    note.NoteMask = noteMaskGreen | note.NoteMask & ~noteMaskOpen;
                    if (currentOpen.IsChild)
                    {
                        currentOpen.NoteMask = noteMaskGreen;
                    }
                    if (currentOpen.IsSustain)
                    {
                        lastGreenSustain = currentOpen;
                        openInLastGreenSustainChordBeforeConversion = true;
                    }
                }
                //PG chords
                else if (currentGreen != null && currentOpen != null)
                {
                    //open note is the parent note
                    if (currentOpen == note)
                    {
                        currentOpen.Fret = FiveFretGuitarFret.Green.Convert();
                        //or the mask with the mask for green and then And the mask with all bits except purple
                        note.NoteMask = note.NoteMask & ~noteMaskOpen;
                        currentOpen.TickLength = Math.Max(currentOpen.TickLength, currentGreen.TickLength);
                        currentOpen.TimeLength = Math.Max(currentOpen.TimeLength, currentGreen.TimeLength);
                        currentOpen.ChildNotes.Remove(currentGreen);
                        if (currentOpen.IsSustain)
                        {
                            lastGreenSustain = currentOpen;
                            openInLastGreenSustainChordBeforeConversion = true;
                        }
                    }
                    //any note other than open note is the parent note
                    else
                    {
                        //or the mask with the mask for green and then And the mask with all bits except purple
                        note.NoteMask = note.NoteMask & ~noteMaskOpen;
                        currentGreen.TickLength = Math.Max(currentOpen.TickLength, currentGreen.TickLength);
                        currentGreen.TimeLength = Math.Max(currentOpen.TimeLength, currentGreen.TimeLength);
                        note.ChildNotes.Remove(currentOpen);
                        if (currentGreen.IsSustain)
                        {
                            lastGreenSustain = currentGreen;
                            openInLastGreenSustainChordBeforeConversion = true;
                        }
                    }
                }
                //green notes just need to be set as last sustain
                else if (currentGreen != null && currentOpen == null)
                {
                    if (currentGreen.IsSustain)
                    {
                        lastGreenSustain = currentGreen;
                        openInLastGreenSustainChordBeforeConversion = false;
                    }
                }

                //set note to strum if it would be a hopo on the same chord
                if ((noteMaskGreen & note.NoteMask) != 0 && lastNoteMask == note.NoteMask && note.IsHopo)
                {
                    note.Type = GuitarNoteType.Strum;
                    if (note.IsParent)
                    {
                        foreach (var childNote in note.ChildNotes)
                        {
                            childNote.Type = GuitarNoteType.Strum;
                        }
                    }
                }

                //reset current notes for next iteration
                currentGreen = null;
                currentOpen = null;
                lastNoteMask = note.NoteMask;
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

            // Bail if the first shift event is after the first note. We could try to guess, but we may well end up
            // with a really bad chart if we do.
            if (difficulty.RangeShiftEvents[0].Time > difficulty.Notes[0].Time)
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

                    if (note.Fret != (int) FiveFretGuitarFret.Open &&
                        (note.Fret > maxFretAllowed || note.Tick < laneEndTicks[note.Fret - shiftAmount]))
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

                    if (note.Fret != (int) FiveFretGuitarFret.Open &&
                        (note.Fret < minFretAllowed || note.Tick < laneEndTicks[note.Fret - shiftAmount]))
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

                // Don't add the trackers for open fret
                if (note.Fret != (int) FiveFretGuitarFret.Open)
                {
                    laneEndTicks[note.Fret] = note.Tick + note.TickLength;
                }

                foreach (var childNote in note.ChildNotes)
                {
                    if (note.Fret != (int) FiveFretGuitarFret.Open)
                    {
                        laneEndTicks[childNote.Fret] = note.Tick + childNote.TickLength;
                    }
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
                        // we have to move it to the NEXT note and adjust the activation phrase end.
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].ActivateFlag(DrumNoteFlags.StarPowerActivator);
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.ActivateFlag(DrumNoteFlags.StarPowerActivator);
                            }
                        }

                        Phrase? activationPhrase = null;

                        foreach (var phrase in difficulty.Phrases)
                        {
                            if (phrase.Type == PhraseType.DrumFill && phrase.Time < note.Time && phrase.TimeEnd >= note.Time)
                            {
                                activationPhrase = phrase;
                                break;
                            }
                        }

                        if (activationPhrase != null)
                        {
                            activationPhrase.TimeLength = difficulty.Notes[index].Time - activationPhrase.Time;
                            activationPhrase.TickLength = difficulty.Notes[index].Tick - activationPhrase.Tick;
                        }
                    }

                    if (note.IsSoloStart && !note.IsSoloEnd)
                    {
                        // This is a single kick drum note that is a solo start, we have to move it to the
                        // NEXT note (we don't want to extend the solo).
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].ActivateFlag(NoteFlags.SoloStart);
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.ActivateFlag(NoteFlags.SoloStart);
                            }
                        }
                    }

                    if (note.IsSoloEnd)
                    {
                        // This is a single kick drum note that is a solo end, we have to move it to the
                        // PREVIOUS note (we don't want to extend the solo).
                        if (index > 0)
                        {
                            difficulty.Notes[index - 1].ActivateFlag(NoteFlags.SoloEnd);
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index - 1].ChildNotes)
                            {
                                childNote.ActivateFlag(NoteFlags.SoloEnd);
                            }
                        }
                    }

                    if (note.IsStarPowerStart && !note.IsStarPowerEnd)
                    {
                        // This is a single kick drum note that is a starpower start, we have to move it to the
                        // NEXT note (we don't want to extend the starpower section).
                        if (index < difficulty.Notes.Count)
                        {
                            difficulty.Notes[index].ActivateFlag(NoteFlags.StarPowerStart);
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index].ChildNotes)
                            {
                                childNote.ActivateFlag(NoteFlags.StarPowerStart);
                            }
                        }
                    }

                    if (note.IsStarPowerEnd)
                    {
                        // This is a single kick drum note that is a starpower end, we have to move it to the
                        // PREVIOUS note (we don't want to extend the starpower section).
                        if (index > 0)
                        {
                            difficulty.Notes[index - 1].ActivateFlag(NoteFlags.StarPowerEnd);
                            // Also add it to the child notes
                            foreach (var childNote in difficulty.Notes[index - 1].ChildNotes)
                            {
                                childNote.ActivateFlag(NoteFlags.StarPowerEnd);
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

        public static void SetDrumActivationFlags(this InstrumentDifficulty<DrumNote> difficulty, StarPowerActivationType activationType)
        {
            difficulty.ClearDrumActivationFlags();

            var notes = difficulty.Notes;

            // Use checkpointing to only iterate through the notes once
            int checkpoint = 0;

            foreach (var phrase in difficulty.Phrases)
            {

                if (phrase.Type != PhraseType.DrumFill)
                {
                    continue;
                }

                for (int i = checkpoint; i < notes.Count; i++)
                {
                    checkpoint = i;

                    // If the current note is outside of the target phrase or if we have exhausted all notes
                    if (notes[i].Time >= phrase.TimeEnd || i == notes.Count - 1)
                    {
                        // Get the rightmost pad
                        var rightmostNote = notes[i].ParentOrSelf;
                        foreach (var note in notes[i].AllNotes)
                        {
                            if (note.Pad > rightmostNote.Pad)
                            {
                                rightmostNote = note;
                            }

                            // Set every note on this tick as an activation note in the case of AllNotes
                            if (activationType == StarPowerActivationType.AllNotes)
                            {
                                note.ActivateFlag(DrumNoteFlags.StarPowerActivator);
                            }
                        }

                        // Only set the rightmost activation note in the case of RightmostNote
                        if (activationType == StarPowerActivationType.RightmostNote)
                        {
                            rightmostNote.ActivateFlag(DrumNoteFlags.StarPowerActivator);
                        }

                        break;
                    }
                }
            }

        }

        public static void ClearDrumActivationFlags(this InstrumentDifficulty<DrumNote> difficulty)
        {
            foreach (var chord in difficulty.Notes)
            {
                foreach (var note in chord.AllNotes)
                {
                    note.ClearFlag(DrumNoteFlags.StarPowerActivator);
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
