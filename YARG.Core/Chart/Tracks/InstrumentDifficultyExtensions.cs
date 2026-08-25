using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Engine.Guitar;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class InstrumentDifficultyExtensions
    {
        /// <summary>
        /// Converts a 5-fret guitar difficulty into a 6-fret copy suitable for six-fret gameplay.
        ///
        /// The 5-fret and 6-fret fret values coincide numerically (Green-Orange = Black1-White2,
        /// Open is shared), so most notes map 1:1. However, a naive mapping can produce illegal
        /// or unplayable 6-fret chords: two notes sharing a lane form a barre, and charting rules
        /// forbid any other chord note in a lane to the LEFT of a barre.
        ///
        /// Conversion therefore walks the track sequentially, mirroring how human charters re-chart:
        /// each chord is placed at the legal, fret-order-preserving position that best preserves
        /// the chart's movement (scored in lane space), keeps anchor phrases hold-and-tap playable,
        /// and avoids stacking sustains or barre hopos. See
        /// Docs/5fret_to_6fret_conversion.md for the full description.
        ///
        /// A legal 6-fret chord can hold at most 4 notes (one barre plus one note per lane to its
        /// right), so the 5-note open chord has no legal placement; its Orange member is dropped.
        /// </summary>
        public static InstrumentDifficulty<GuitarNote> ConvertFiveFretToSixFret(this InstrumentDifficulty<GuitarNote> difficulty)
        {
            var converted = new InstrumentDifficulty<GuitarNote>(difficulty);

            double previousLane = -1;
            double previousIdentityCentroid = -1;
            var previousPlacedFrets = new List<int>();
            var previousIdentityFrets = new List<int>();
            var activeSustains = new List<(uint TickEnd, int PlacedFret, int IdentityFret)>();
            for (int i = 0; i < converted.Notes.Count; i++)
            {
                // One-note lookahead: avoid placements that would push the next chart step off
                // the highway (e.g. a rising run pinning itself at White3)
                double? nextIdentityCentroid = i + 1 < converted.Notes.Count
                    ? GetFrettedIdentityCentroid(converted.Notes[i + 1])
                    : null;

                (previousLane, previousIdentityCentroid) = PlaceChordLegally(converted.Notes[i],
                    previousLane, previousIdentityCentroid, previousPlacedFrets, previousIdentityFrets,
                    activeSustains, nextIdentityCentroid);
            }
            return converted;
        }

        /// <summary>Masks for the six fretted 6-fret notes (Black1 through White3).</summary>
        private const int SIX_FRET_FRETS_MASK = 0x3F;

        /// <summary>
        /// Movement is scored in LANE space, not fret space: the 6-fret highway snakes (black row
        /// left-to-right, then white row), so a fret-monotonic sequence visually zig-zags. Half a
        /// lane per fret step makes rising 5-fret runs sweep across the three lanes in order.
        /// </summary>
        private const double LANES_PER_FRET = 0.5;

        /// <summary>Weight of lane distance in the placement score; dominates fret distance.</summary>
        private const double LANE_WEIGHT = 2.0;

        /// <summary>Weight of fret distance; tie-breaks between placements sharing a lane.</summary>
        private const double FRET_WEIGHT = 0.125;

        /// <summary>
        /// Penalty for re-striking the exact previous position on a distinct chart step, so forced
        /// repeats at least flip rows (e.g. W2 to B2) and read as a new note.
        /// </summary>
        private const double REPEAT_POSITION_PENALTY = 0.25;

        /// <summary>
        /// Penalty for moving a fret that the previous chord also contains. Chords sharing a 5-fret
        /// note with an adjacent single (e.g. YB,Y,Y,YB) are anchor phrases: the player holds the
        /// non-shared note and taps the shared one, which only works if the shared note keeps its
        /// placed fret across the phrase.
        /// </summary>
        private const double SHARED_ANCHOR_PENALTY = 2.0;

        private static readonly int[] SixFretLaneMasks =
        {
            (1 << ((int) SixFretGuitarFret.Black1 - 1)) | (1 << ((int) SixFretGuitarFret.White1 - 1)),
            (1 << ((int) SixFretGuitarFret.Black2 - 1)) | (1 << ((int) SixFretGuitarFret.White2 - 1)),
            (1 << ((int) SixFretGuitarFret.Black3 - 1)) | (1 << ((int) SixFretGuitarFret.White3 - 1)),
        };

        /// <summary>
        /// Centroid of a chord's fretted members under the direct mapping, or null if the chord has
        /// none (pure open/wildcard). Used for movement scoring and lookahead.
        /// </summary>
        private static double? GetFrettedIdentityCentroid(GuitarNote note)
        {
            double sum = 0;
            int count = 0;
            foreach (var member in note.AllNotes)
            {
                if (member.Fret is (int) FiveFretGuitarFret.Open or (int) FiveFretGuitarFret.Wildcard)
                {
                    continue;
                }

                sum += member.Fret;
                count++;
            }

            return count > 0 ? sum / count : null;
        }

        private static (double placedLane, double identityCentroid) PlaceChordLegally(GuitarNote note,
            double previousLane, double previousIdentityCentroid, List<int> previousPlacedFrets,
            List<int> previousIdentityFrets, List<(uint TickEnd, int PlacedFret, int IdentityFret)> activeSustains,
            double? nextIdentityCentroid)
        {
            // Collect fretted members; Open/Wildcard members keep their shared value and are ignored
            // for legality (they span all lanes rather than living in one).
            List<GuitarNote>? fretted = null;
            foreach (var member in note.AllNotes)
            {
                if (member.Fret is (int) FiveFretGuitarFret.Open or (int) FiveFretGuitarFret.Wildcard)
                {
                    // Open/Wildcard keep their shared value; ignored for legality since they
                    // span all lanes rather than living in one. Their NoteMask bit survives.
                }
                else
                {
                    (fretted ??= new List<GuitarNote>()).Add(member);
                }
            }

            // No fretted members (pure open/wildcard note): identity is always legal, and the
            // open note doesn't move the hand, so the previous placement stays the reference
            if (fretted == null || fretted.Count == 0)
            {
                return (previousLane, previousIdentityCentroid);
            }

            // A legal 6-fret chord holds at most 4 fretted notes; drop Orange from 5-note chords
            if (fretted.Count > 4)
            {
                RemoveOrangeMember(note, fretted);
            }

            // Members sorted by fret; candidates assign increasing 6-fret values (order-preserving)
            var members = fretted;
            members.Sort((a, b) => a.Fret.CompareTo(b.Fret));

            // Where the direct mapping would place this chord (5-fret values coincide with 6-fret)
            double identitySum = 0;
            foreach (var member in members)
            {
                identitySum += member.Fret;
            }
            double identityCentroid = identitySum / members.Count;

            // Ideal lane preserves the chart's own movement: the previously placed lane plus the
            // fret step the 5-fret chart makes, scaled at half a lane per fret. Exact halves push
            // in the direction of motion so runs sweep lane-by-lane instead of hovering.
            double idealLane;
            double fretStep = 0;
            if (previousLane < 0 || previousIdentityCentroid < 0)
            {
                idealLane = 0;
                foreach (var member in members)
                {
                    idealLane += LaneOf(member.Fret);
                }
                idealLane /= members.Count;
            }
            else
            {
                fretStep = identityCentroid - previousIdentityCentroid;
                idealLane = previousLane + fretStep * LANES_PER_FRET;
                double frac = idealLane - Math.Floor(idealLane);
                if (Math.Abs(frac - 0.5) < 1e-9 && fretStep != 0)
                {
                    idealLane += 0.5 * Math.Sign(fretStep);
                }
            }

            // Lanes of notes still sustaining across this chord must not be struck into: a second
            // sustain line would stack onto the first. Exception: a chord that CONTINUES an active
            // sustain (places that same 5-fret note at the same fret) may share its lane — that is
            // the anchor-pattern realization (e.g. sustained Y, then YB chords tapping around it).
            // When every lane is blocked by foreign sustains, the fallback pass places nearest.
            activeSustains.RemoveAll(s => s.TickEnd <= note.Tick);

            // Hopo rule: no pull-offs from a barre into a single in the barre's own lane, and no
            // hammer-ons from a single onto a barre in the single's own lane. Both directions are
            // unplayable/ambiguous on a barre lane pair.
            int previousMask = 0;
            foreach (var fret in previousPlacedFrets)
            {
                previousMask |= 1 << (fret - 1);
            }
            int previousBarreLanes = GetBarreLanes(previousMask);
            bool previousIsSingle = previousPlacedFrets.Count == 1;
            int previousSingleLane = previousIsSingle ? LaneOf(previousPlacedFrets[0]) : -1;

            var candidates = EnumerateIncreasingAssignments(members.Count);

            // Whether the candidate strikes into a lane occupied by a sustain it does not continue
            bool UsesForeignSustainLane(ReadOnlySpan<int> assignment, int assignmentMask)
            {
                foreach (var (_, placedFret, identityFret) in activeSustains)
                {
                    bool absorbed = false;
                    for (int i = 0; i < members.Count; i++)
                    {
                        if (members[i].Fret == identityFret && assignment[i] == placedFret)
                        {
                            absorbed = true;
                            break;
                        }
                    }

                    if (!absorbed && (assignmentMask & SixFretLaneMasks[LaneOf(placedFret)]) != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            int bestMask = 0;
            double bestScore = double.MaxValue;
            double bestLane = idealLane;
            Span<int> bestAssignment = stackalloc int[members.Count];

            // Pass 0 honors the sustain-collision restriction; pass 1 ignores it as a fallback in
            // case every legal candidate collides
            for (int pass = 0; pass < 2 && bestMask == 0; pass++)
            {
                foreach (var candidate in candidates)
                {
                    int mask = 0;
                    double fretSum = 0;
                    double laneSum = 0;
                    for (int i = 0; i < candidate.Length; i++)
                    {
                        mask |= 1 << (candidate[i] - 1);
                        fretSum += candidate[i];
                        laneSum += LaneOf(candidate[i]);
                    }

                    if (!IsLegalSixFretChord(mask) || (pass == 0 && UsesForeignSustainLane(candidate, mask)))
                    {
                        continue;
                    }

                    // Pass 0 also enforces the barre/single hopo rule (see above)
                    if (pass == 0)
                    {
                        if (previousBarreLanes != 0 && candidate.Length == 1 &&
                            (previousBarreLanes & (1 << LaneOf(candidate[0]))) != 0)
                        {
                            continue;
                        }

                        if (previousIsSingle &&
                            (GetBarreLanes(mask) & (1 << previousSingleLane)) != 0)
                        {
                            continue;
                        }
                    }

                    double fret = fretSum / candidate.Length;
                    double lane = laneSum / candidate.Length;
                    double score = LANE_WEIGHT * Math.Abs(lane - idealLane)
                        + FRET_WEIGHT * Math.Abs(fret - identityCentroid);

                    // A distinct chart step shouldn't re-strike the exact previous position; when
                    // the lane is forced, the penalty at least flips the row (e.g. W2 to B2)
                    if (fretStep != 0 && RepeatsPreviousPosition(candidate, previousPlacedFrets))
                    {
                        score += REPEAT_POSITION_PENALTY;
                    }

                    // Anchor phrases: members shared with the previous chord keep their placed
                    // fret, so the player can hold the non-shared note and tap the shared one
                    for (int i = 0; i < members.Count; i++)
                    {
                        int sharedIndex = previousIdentityFrets.IndexOf(members[i].Fret);
                        if (sharedIndex >= 0 && candidate[i] != previousPlacedFrets[sharedIndex])
                        {
                            score += SHARED_ANCHOR_PENALTY;
                        }
                    }

                    // Lookahead: penalize placements that would force the next chart step off the
                    // highway, so runs approaching an edge shift over early instead of pinning
                    if (nextIdentityCentroid is double nextIdent)
                    {
                        double nextIdealLane = lane + (nextIdent - identityCentroid) * LANES_PER_FRET;
                        score += Math.Max(0, nextIdealLane - 2);
                        score += Math.Max(0, -nextIdealLane);
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMask = mask;
                        bestLane = lane;
                        candidate.CopyTo(bestAssignment);
                    }
                }
            }

            // Apply the chosen assignment
            previousPlacedFrets.Clear();
            previousIdentityFrets.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                // Record the 5-fret identity BEFORE SetSixFret overwrites it with the 6-fret value
                int identityFret = members[i].Fret;
                SetSixFret(members[i], bestAssignment[i]);
                previousPlacedFrets.Add(bestAssignment[i]);
                previousIdentityFrets.Add(identityFret);

                // Track sustains so later chords avoid their lanes while they are still held
                if (members[i].TickLength > 0)
                {
                    activeSustains.Add((members[i].Tick + members[i].TickLength,
                        bestAssignment[i], identityFret));
                }
            }

            // Parent NoteMask: replace the fretted bits with the chosen ones, keep open-like bits
            int openBits = note.NoteMask & ~SIX_FRET_FRETS_MASK;
            note.NoteMask = openBits | bestMask;

            return (bestLane, identityCentroid);
        }

        /// <summary>Lane index (0-2) of a fretted 6-fret value.</summary>
        private static int LaneOf(int fret)
        {
            return (fret - 1) % 3;
        }

        /// <summary>Bitmask of lanes (bit 0-2) that contain a barre (both members) in the given mask.</summary>
        private static int GetBarreLanes(int fretMask)
        {
            int lanes = 0;
            for (int lane = 0; lane < 3; lane++)
            {
                if ((fretMask & SixFretLaneMasks[lane]) == SixFretLaneMasks[lane])
                {
                    lanes |= 1 << lane;
                }
            }

            return lanes;
        }

        private static bool RepeatsPreviousPosition(ReadOnlySpan<int> candidate, List<int> previousPlacedFrets)
        {
            if (candidate.Length != previousPlacedFrets.Count)
            {
                return false;
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] != previousPlacedFrets[i])
                {
                    return false;
                }
            }

            return true;
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

        /// <summary>
        /// Whether the given fretted-bit mask forms a legal 6-fret chord: no note may sit in a
        /// lane to the left of any barre (two notes sharing a lane).
        /// </summary>
        private static bool IsLegalSixFretChord(int mask)
        {
            for (int lane = 0; lane < SixFretLaneMasks.Length; lane++)
            {
                if ((mask & SixFretLaneMasks[lane]) != SixFretLaneMasks[lane])
                {
                    continue;
                }

                // Barre in this lane; nothing may exist in lanes to its left
                for (int left = 0; left < lane; left++)
                {
                    if ((mask & SixFretLaneMasks[left]) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Enumerates all ways to assign <paramref name="count"/> distinct 6-fret values (1-6) in
        /// increasing order, preserving the chord's fret ordering. Returns arrays of fret values.
        /// </summary>
        private static List<int[]> EnumerateIncreasingAssignments(int count)
        {
            var results = new List<int[]>(15);
            var current = new int[count];
            void Build(int index, int min)
            {
                if (index == count)
                {
                    results.Add((int[]) current.Clone());
                    return;
                }

                for (int value = min; value <= (int) SixFretGuitarFret.White3 - (count - 1 - index); value++)
                {
                    current[index] = value;
                    Build(index + 1, value + 1);
                }
            }

            Build(0, 1);
            return results;
        }

        /// <summary>
        /// Removes the Orange member of a 5-note chord so the remainder can be placed legally.
        /// </summary>
        private static void RemoveOrangeMember(GuitarNote note, List<GuitarNote> frettedMembers)
        {
            const int orangeMask = 1 << ((int) FiveFretGuitarFret.Orange - 1);

            frettedMembers.RemoveAll(m => m.Fret == (int) FiveFretGuitarFret.Orange);
            if (note.Fret != (int) FiveFretGuitarFret.Orange)
            {
                // Orange is a child; detach it
                for (int i = note.ChildNotes.Count - 1; i >= 0; i--)
                {
                    if (note.ChildNotes[i].Fret == (int) FiveFretGuitarFret.Orange)
                    {
                        note.ChildNotes.RemoveAt(i);
                    }
                }
                note.NoteMask &= ~orangeMask;
                return;
            }

            // Orange is the parent: repoint it at the first surviving member and re-add the rest
            note.ChildNotes.Clear();
            if (frettedMembers.Count == 0)
            {
                note.NoteMask &= ~orangeMask;
                note.DisjointMask &= ~orangeMask;
                return;
            }

            note.Fret = frettedMembers[0].Fret;
            note.DisjointMask = frettedMembers[0].DisjointMask;
            note.NoteMask = 0;
            foreach (var member in frettedMembers)
            {
                note.AddChildNote(member);
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

            // return difficulty;
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