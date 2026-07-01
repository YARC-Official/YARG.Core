using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Keys
{
    public abstract class FiveLaneKeysEngine : KeysEngine<GuitarNote>
    {
        public enum FiveLaneKeysAction {
            GreenKey = 0,
            RedKey = 1,
            YellowKey = 2,
            BlueKey = 3,
            OrangeKey = 4,
            // 5 intentionally left blank for symmetry with 6F
            OpenNote = 6,
            Wildcard = 7,
        }

        public bool IsKeyHeld(FiveLaneKeysAction key)
        {
            return (KeyMask & (1 << (int)key)) != 0;
        }

        protected override double[] KeyPressTimes { get; } = new double[8];

        protected FiveLaneKeysEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
            GetWaitCountdowns(Notes);
        }

        protected override bool CanSustainHold(GuitarNote note)
        {
            return (KeyMask & note.DisjointMask) != 0 ||
                (KeyMask > 0 && note.FiveLaneKeysAction is FiveLaneKeysAction.Wildcard);
        }

        protected override void HitNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.FiveLaneKeysAction, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            bool partiallyHit = false;
            foreach (var child in note.ParentOrSelf.AllNotes)
            {
                if (child.WasHit || child.WasMissed)
                {
                    partiallyHit = true;
                    break;
                }
            }

            note.SetHitState(true, false);

            KeyPressTimes[(int)note.FiveLaneKeysAction] = DEFAULT_PRESS_TIME;

            // Cancel rest of hit logic during BRE phrase. Key on CodaHasStarted, not IsCodaActive
            // (see DrumsEngine.HitNote): an end-tick finale is judged after the coda's EndTime,
            // where IsCodaActive is already false.
            if (CodaHasStarted && note.IsBigRockEnding)
            {
                base.HitNote(note);
                return;
            }

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
            {
                AwardStarPower(note);
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (IsSoloActive)
            {
                Solos[CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (note.ParentOrSelf.WasFullyHit())
            {
                ChordStaggerTimer.Disable(CurrentTime, early: true);
            }

            // Only increase combo for the first note in a chord
            if (!partiallyHit)
            {
                IncrementCombo();
            }

            EngineStats.IncrementNotesHit(note, CurrentTime);

            UpdateMultiplier();

            AddScore(note);

            if (note.IsSustain)
            {
                StartSustain(note);
            }

            OnNoteHit?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.FiveLaneKeysAction, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            KeyPressTimes[(int)note.FiveLaneKeysAction] = DEFAULT_PRESS_TIME;

            // Can't miss a note during the coda. Key on CodaHasStarted, not IsCodaActive (see
            // DrumsEngine.MissNote): the miss is judged at the back-end time, which for an
            // end-tick finale falls after the coda's EndTime where IsCodaActive is already false.
            if (CodaHasStarted && note.IsBigRockEnding)
            {
                // Resolve the whole chord, matching GuitarEngine (see DrumsEngine.MissNote).
                note.SetHitState(true, true);
                base.HitNote(note);
                return;
            }

            note.SetMissState(true, false);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note is { IsSoloStart: true, IsSoloEnd: true } && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                // While a solo is active, end the current solo and immediately start the next.
                if (IsSoloActive)
                {
                    EndSolo();
                    StartSolo();
                }
                else
                {
                    // If no solo is currently active, start and immediately end the solo.
                    StartSolo();
                    EndSolo();
                }
            }
            else if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }
            else if (note.IsSoloStart)
            {
                StartSolo();
            }

            // If no notes within a chord were hit, combo is 0
            if (note.ParentOrSelf.WasFullyMissed())
            {
                ResetCombo();
            }
            else
            {
                // If any of the notes in a chord were hit, the combo for that note is rewarded, but it is reset back to 1
                ResetCombo();
                IncrementCombo();
            }

            UpdateMultiplier();

            if (CodaHasStarted)
            {
                Codas[CurrentCodaIndex].MissNote();
            }

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.MissNote(note);
        }

        protected override void AddScore(GuitarNote note)
        {
            int scoredNotePoints = ApplyAccuracyScore(note, POINTS_PER_NOTE);

            AddScore(scoredNotePoints);
            EngineStats.NoteScore += scoredNotePoints;
        }

        protected sealed override (int baseScore, int noteScore) CalculateChartScores()
        {
            double baseScore = 0;
            double noteScore = 0;
            int combo = 0;
            int multiplier;
            foreach (var note in Notes)
            {
                // Exclude BRE notes from base score calculation since they can't be scored
                if (note.IsBigRockEnding)
                {
                    continue;
                }

                // Get the current multiplier given the current combo
                multiplier = Math.Min((combo / 10) + 1, BaseParameters.MaxMultiplier);
                double scoreForNote = POINTS_PER_NOTE * (1 + note.ChildNotes.Count);

                foreach (var child in note.AllNotes)
                {
                    scoreForNote += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
                baseScore += multiplier * scoreForNote;
                noteScore += scoreForNote;

                double pointsForSustain = Math.Ceiling(note.TickLength / TicksPerSustainPoint);
                baseScore += multiplier * pointsForSustain;
                noteScore += pointsForSustain;
                combo++;
                // If a note is disjoint, each sustain is counted separately.
                if (note.IsDisjoint)
                {
                    foreach (var child in note.ChildNotes)
                    {
                        HashSet<uint> seenNoteTicks = new();
                        double pointsForDisjoint = Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                        baseScore += multiplier * pointsForDisjoint;
                        noteScore += pointsForDisjoint;
                        // Only increment combo if we haven't already seen a note in that tick
                        if (seenNoteTicks.Add(child.Tick))
                        {
                            combo++;
                        }
                    }
                }
                combo++;
            }

            YargLogger.LogDebug($"[Keys] Base score: {baseScore}, Max Combo: {combo}");
            return ((int) Math.Round(baseScore), (int) Math.Round(noteScore));
        }

        protected override int CalculateMaxScoreWithoutStarPower()
        {
            double maxScore = 0;
            int combo = 0;
            foreach (var note in Notes)
            {
                if (note.IsBigRockEnding)
                {
                    continue;
                }

                combo++;
                int multiplier = GetScoreMultiplierForCombo(combo);
                foreach (var child in note.AllNotes)
                {
                    maxScore += multiplier * POINTS_PER_NOTE;
                    maxScore += multiplier * Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
            }

            return (int) Math.Round(maxScore) + EngineStats.MaxSoloBonusPoints + CalculateTotalCodaBonus();
        }

        // protected override bool IsKeyInTime(GuitarNote note, double frontEnd) => IsKeyInTime(note, (int)note.FiveLaneKeysAction, frontEnd);

        protected override bool IsKeyInTime(GuitarNote note, double frontEnd)
        {
            if (note.Fret != (int) FiveFretGuitarFret.Wildcard)
            {
                return IsKeyInTime(note, (int) note.FiveLaneKeysAction, frontEnd);
            }

            // Check that any key was pressed within the front end
            // TODO: Eliminate this loop by tracking a global LastKeyPressTime or something
            foreach (var pressTime in KeyPressTimes)
            {
                if (pressTime > note.Time + frontEnd)
                {
                    return true;
                }
            }

            return false;
        }

        protected FiveLaneKeysAction ProKeysActionToFiveLaneKeysAction(ProKeysAction action)
        {
            return action switch
            {
                ProKeysAction.GreenKey => FiveLaneKeysAction.GreenKey,
                ProKeysAction.RedKey => FiveLaneKeysAction.RedKey,
                ProKeysAction.YellowKey => FiveLaneKeysAction.YellowKey,
                ProKeysAction.BlueKey => FiveLaneKeysAction.BlueKey,
                ProKeysAction.OrangeKey => FiveLaneKeysAction.OrangeKey,
                ProKeysAction.OpenNote => FiveLaneKeysAction.OpenNote,
                _ => throw new Exception("Unhandled")
            };
        }
    }
}
