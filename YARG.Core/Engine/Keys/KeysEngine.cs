using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Logging;

namespace YARG.Core.YARG.Core.Engine.ProKeys
{
    public abstract class KeysEngine<TNoteType> : BaseEngine<TNoteType, KeysEngineParameters, ProKeysStats>
        where TNoteType : Note<TNoteType>
    {
        protected struct KeyPressedTimes
        {
            public int NoteIndex;
            public double Time;
        }

        protected const double DEFAULT_PRESS_TIME = -9999;
        protected EngineTimer ChordStaggerTimer;
        protected EngineTimer FatFingerTimer;

        public delegate void KeyStateChangeEvent(int key, bool isPressed);
        public delegate void OverhitEvent(int key);

        public KeyStateChangeEvent? OnKeyStateChange;

        public OverhitEvent? OnOverhit;

        // Used for hit logic. May not be the same value as KeyHeldMask
        public int KeyMask { get; protected set; }

        public int PreviousKeyMask { get; protected set; }

        protected abstract double[] KeyPressTimes { get; }

        /// <summary>
        /// The integer value for the key that was hit this update. <c>null</c> is none.
        /// </summary>
        protected int? KeyHitThisUpdate;

        /// <summary>
        /// The integer value for the key that was released this update. <c>null</c> is none.
        /// </summary>
        protected int? KeyReleasedThisUpdate;

        protected TNoteType? FatFingerNote;

        protected int? FatFingerKey;

        protected KeysEngine(InstrumentDifficulty<TNoteType> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, true, isBot)
        {
            ChordStaggerTimer = new("Chord Stagger", engineParameters.ChordStaggerWindow);
            FatFingerTimer = new("Fat Finger", engineParameters.FatFingerWindow);
        }

        public EngineTimer GetChordStaggerTimer() => ChordStaggerTimer;
        public EngineTimer GetFatFingerTimer() => FatFingerTimer;

        public ReadOnlySpan<double> GetKeyPressTimes() => KeyPressTimes;

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = CurrentTime;

            if (ChordStaggerTimer.IsActive)
            {
                if (IsTimeBetween(ChordStaggerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing chord stagger end time at {0}", ChordStaggerTimer.EndTime);
                    QueueUpdateTime(ChordStaggerTimer.EndTime, "Chord Stagger End");
                }
            }

            if (FatFingerTimer.IsActive)
            {
                if (IsTimeBetween(FatFingerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing fat finger end time at {0}", FatFingerTimer.EndTime);
                    QueueUpdateTime(FatFingerTimer.EndTime, "Fat Finger End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            KeyMask = 0;

            var neampgfk = KeyPressTimes.Length;

            for (int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }

            KeyHitThisUpdate = null;
            KeyReleasedThisUpdate = null;

            FatFingerKey = null;

            ChordStaggerTimer.Reset();
            FatFingerTimer.Reset();

            FatFingerNote = null;

            base.Reset(keepCurrentButtons);
        }

        protected virtual void Overhit(int key)
        {
            // Can't overstrum before first note is hit/missed
            if (NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Cancel overstrum if WaitCountdown is active
            if (IsWaitCountdownActive)
            {
                YargLogger.LogFormatTrace("Overstrum prevented during WaitCountdown at time: {0}, tick: {1}", CurrentTime, CurrentTick);
                return;
            }

            YargLogger.LogFormatTrace("Overhit at {0}", CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), CurrentTime);
                i--;

                double finalScore = CalculateSustainPoints(ref sustain, CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, CurrentTime, sustain.HasFinishedScoring);
            }

            if (NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[NoteIndex]);
                }
            }

            ResetCombo();
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke(key);
        }

        protected abstract override bool CanSustainHold(TNoteType note);

        protected void ToggleKey(int key, bool active)
        {
            KeyMask = active ? KeyMask | (1 << key) : KeyMask & ~(1 << key);
        }

        protected bool IsKeyInTime(TNoteType note, int key, double frontEnd)
        {
            return KeyPressTimes[key] > note.Time + frontEnd;
        }

        protected abstract bool IsKeyInTime(TNoteType note, double frontEnd);
    }
}
