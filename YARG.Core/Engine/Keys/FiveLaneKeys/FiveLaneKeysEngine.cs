using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.YARG.Core.Engine.ProKeys
{
    public abstract class FiveLaneKeysEngine : KeysEngine<GuitarNote>
    {
        public enum FiveLaneKeysAction {
            GreenKey = 0,
            RedKey = 1,
            YellowKey = 2,
            BlueKey = 3,
            OrangeKey = 4,
            
            OpenNote = 6
        }

        public bool IsKeyHeld(FiveLaneKeysAction key)
        {
            return (KeyMask & (1 << (int)key)) != 0;
        }

        protected override double[] KeyPressTimes { get; } = new double[7];

        protected FiveLaneKeysEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override bool CanSustainHold(GuitarNote note)
        {
            return (KeyMask & note.DisjointMask) != 0;
        }
        protected override void HitNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Fret, NoteIndex, note.WasHit, note.WasMissed);
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

            KeyPressTimes[note.Fret] = DEFAULT_PRESS_TIME;

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
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

            EngineStats.NotesHit++;

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
                    note.Fret, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetMissState(true, false);

            KeyPressTimes[(int)note.FiveLaneKeysAction] = DEFAULT_PRESS_TIME;

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

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void AddScore(GuitarNote note)
        {
            AddScore(POINTS_PER_NOTE);
            EngineStats.NoteScore += POINTS_PER_NOTE;
        }

        protected sealed override int CalculateBaseScore()
        {
            double score = 0;
            int combo = 0;
            int multiplier;
            double weight;
            foreach (var note in Notes)
            {
                // Get the current multiplier given the current combo
                multiplier = Math.Min((combo / 10) + 1, BaseParameters.MaxMultiplier);

                // invert it to calculate leniency
                weight = 1.0 * multiplier / BaseParameters.MaxMultiplier;
                score += weight * (POINTS_PER_NOTE * (1 + note.ChildNotes.Count));

                foreach (var child in note.AllNotes)
                {
                    score += weight * (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }

                // Pro Keys combo increments per chord, not per note.
                combo++;
            }

            YargLogger.LogDebug($"[Pro Keys] Base score: {score}, Max Combo: {combo}");
            return (int) Math.Round(score);
        }

        protected override bool IsKeyInTime(GuitarNote note, double frontEnd) => IsKeyInTime(note, (int)note.FiveLaneKeysAction, frontEnd);

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
