using YARG.Core.Chart;
using System;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        private const float INITIAL_CROWD_HAPPINESS        = 0.85f;
        private const float HAPPINESS_FAIL_THRESHOLD       = 0.0f;
        private const float HAPPINESS_PER_NOTE_HIT         = 0.01f;
        private const float HAPPINESS_PER_NOTE_MISS        = 0.04f;
        private const float HAPPINESS_PER_OVERSTRUM        = HAPPINESS_PER_NOTE_MISS / 2;
        private const int   HAPPINESS_STARPOWER_MULTIPLIER = 5;

        private const float HAPPINESS_CROWD_THRESHOLD      = 0.83f;
        public        float Happiness => GetAverageHappiness();

        private int _starpowerCount = 0;

        public        bool IsAnyStarpowerActive => _starpowerCount > 0;

        private bool CheckForFail()
        {
            if (Happiness < HAPPINESS_FAIL_THRESHOLD)
            {
                return true;
            }

            return false;
        }

        private float GetLowestHappiniess()
        {
            float happiness = 1.0f;
            foreach (var engine in _allEngines)
            {
                if (engine.Happiness < happiness)
                {
                    happiness = engine.Happiness;
                }
            }
            return happiness;
        }

        private float GetAverageHappiness()
        {
            float happiness = 0.0f;
            foreach (var engine in _allEngines)
            {
                happiness += engine.Happiness;
            }

            return happiness / _allEngines.Count;
        }

        public partial class EngineContainer
        {
            public float Happiness { get; private set; } = INITIAL_CROWD_HAPPINESS;
            private float _previousHappiness = INITIAL_CROWD_HAPPINESS;

            public delegate void SongFailed();

            public delegate void HappinessOverThreshold();

            public delegate void HappinessUnderThreshold();

            public SongFailed? OnSongFailed;
            public HappinessOverThreshold? OnHappinessOverThreshold;
            public HappinessUnderThreshold? OnHappinessUnderThreshold;

            private void OnNoteHit<TNote>(int index, TNote note) where TNote : Note<TNote>
            {
                // Ignore any notes that have not been fully hit yet on the assumption that a call
                // where the note group was fully hit will eventually come if they are all hit
                if (!note.WasFullyHit() && note is not VocalNote)
                {
                    return;
                }


                if (_engineManager.IsAnyStarpowerActive)
                {
                    Happiness = Math.Clamp(Happiness + HAPPINESS_PER_NOTE_HIT * HAPPINESS_STARPOWER_MULTIPLIER, -1.5f, 1f);
                }
                else
                {
                    Happiness = Math.Clamp(Happiness + HAPPINESS_PER_NOTE_HIT, -1.5f, 1f);
                }

                // Send over threshold event when happiness goes from below threshold to above
                if (Happiness >= HAPPINESS_CROWD_THRESHOLD && _previousHappiness < HAPPINESS_CROWD_THRESHOLD)
                {
                    OnHappinessOverThreshold?.Invoke();
                }

                _previousHappiness = Happiness;
            }

            private void OnNoteMissed<TNote>(int index, TNote note) where TNote : Note<TNote>
            {
                if (!note.WasFullyMissed() && note is not VocalNote)
                {
                    return;
                }
                Happiness = Math.Clamp(Happiness - HAPPINESS_PER_NOTE_MISS, -1.5f, 1f);
                if (_engineManager.CheckForFail())
                {
                    OnSongFailed?.Invoke();
                    YargLogger.LogFormatDebug("Song Fail invoked after miss by player {0} with average happiness {1}", EngineId, _engineManager.Happiness);
                }

                // Send under threshold event when happiness drops from above to below
                if (Happiness < HAPPINESS_CROWD_THRESHOLD && _previousHappiness >= HAPPINESS_CROWD_THRESHOLD)
                {
                    OnHappinessUnderThreshold?.Invoke();
                }

                _previousHappiness = Happiness;
            }

            private void OnOverstrum()
            {
                Happiness = Math.Clamp(Happiness - HAPPINESS_PER_OVERSTRUM, -1.5f, 1f);
                if (_engineManager.CheckForFail())
                {
                    OnSongFailed?.Invoke();
                    YargLogger.LogFormatDebug("Song Fail invoked after overstrum by player {0} with average happiness {1}", EngineId, _engineManager.Happiness);
                }
            }

            private void OnKeysOverhit(int key) => OnOverstrum();

            private void OnStarpowerStatus(bool isActive) => _engineManager._starpowerCount += isActive ? 1 : -1;

            private void SubscribeToEngineEvents()
            {
                // Subscribe to OnNoteHit and OnNoteMissed events
                if (Engine is BaseEngine<GuitarNote,GuitarEngineParameters,GuitarStats> guitarEngine)
                {
                    var engine = (GuitarEngine) guitarEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarpowerStatus;
                    engine.OnOverstrum += OnOverstrum;
                }

                if (Engine is BaseEngine<DrumNote, DrumsEngineParameters, DrumsStats> drumEngine)
                {
                    var engine = (DrumsEngine) drumEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarpowerStatus;
                    engine.OnOverhit += OnOverstrum;
                }

                if (Engine is BaseEngine<ProKeysNote, KeysEngineParameters, KeysStats>
                    proKeysEngine)
                {
                    var engine = (ProKeysEngine) proKeysEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarpowerStatus;
                    engine.OnOverhit += OnKeysOverhit;
                }

                if (Engine is BaseEngine<GuitarNote, KeysEngineParameters, KeysStats> keysEngine)
                {
                    var engine = (FiveLaneKeysEngine) keysEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarpowerStatus;
                    engine.OnOverhit += OnKeysOverhit;
                }

                if (Engine is BaseEngine<VocalNote, VocalsEngineParameters, VocalsStats> vocalsEngine)
                {
                    vocalsEngine.OnNoteHit += OnNoteHit;
                    vocalsEngine.OnNoteMissed += OnNoteMissed;
                    vocalsEngine.OnStarPowerStatus += OnStarpowerStatus;
                }
            }
        }
    }
}