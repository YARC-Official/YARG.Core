using System;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys
{
    public abstract class ProKeysEngine : BaseEngine<ProKeysNote, ProKeysEngineParameters,
        ProKeysStats, ProKeysEngineState>
    {
        public Action? OnOverhit;

        protected ProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            var keys = State.KeyMask;

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                State.KeyMask = keys;
            }
        }

        protected virtual void Overhit()
        {
            // Can't overstrum before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (State.NoteIndex >= Chart.Notes.Count /*&& ActiveSustains.Count == 0*/)
            {
                return;
            }

            YargLogger.LogFormatTrace("Overhit at {0}", State.CurrentTime);

            if (State.NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[State.NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[State.NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke();
        }
    }
}