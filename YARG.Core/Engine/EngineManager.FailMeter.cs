using YARG.Core.Chart;
using System;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        private const float HAPPINESS_FAIL_THRESHOLD        = 0.0f;
        private const float HAPPINESS_PER_NOTE_HIT          = 0.01f;
        private const float HAPPINESS_PER_NOTE_MISS         = 0.04f;

        // The amount of happiness gained if a vocal phrase is completed with an AWESOME rating.
        private const float HAPPINESS_PER_VOCAL_PHRASE_HIT  = 0.1f;

        // The amount of happiness lost if a vocal phrase is missed completely
        private const float HAPPINESS_PER_VOCAL_PHRASE_MISS = 0.2f;

        // The hit percent at which no happiness is gained or lost for a vocal phrase
        private const float VOCAL_HIT_PERC_MIDPOINT         = 0.75f;

        private const float HAPPINESS_CROWD_THRESHOLD       = 0.83f;
        private const float HAPPINESS_MINIMUM               = -1.5f;  
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
            public float Happiness { get; private set; } = 0.0f;
            private float _previousHappiness = 0.0f;

            public delegate void SongFailed();

            public delegate void HappinessOverThreshold();

            public delegate void HappinessUnderThreshold();

            public SongFailed? OnSongFailed;
            public HappinessOverThreshold? OnHappinessOverThreshold;
            public HappinessUnderThreshold? OnHappinessUnderThreshold;

            private void OnVocalPhraseHit(double hitPercentAfterParams, bool fullPoints)
            {
                hitPercentAfterParams = Math.Clamp(hitPercentAfterParams, 0.0, 1.0);
                var delta = 0.0f;

                // If the hit percent is below the midpoint, the player loses happiness based on how far they are from the midpoint
                if (hitPercentAfterParams < VOCAL_HIT_PERC_MIDPOINT)
                {
                    delta = -1 * HAPPINESS_PER_VOCAL_PHRASE_MISS * RockMeterPreset.VocalsMissDamageMultiplier;
                    delta *= 1 - YargMath.InverseLerpF(0.0f, VOCAL_HIT_PERC_MIDPOINT, hitPercentAfterParams);
                }
                // If the hit percent is above the midpoint, the player gains happiness based on how far they are from the midpoint
                else
                {
                    delta = HAPPINESS_PER_VOCAL_PHRASE_HIT * RockMeterPreset.VocalsHitRecoveryMultiplier;
                    delta *= YargMath.InverseLerpF(VOCAL_HIT_PERC_MIDPOINT, 1.0f, hitPercentAfterParams);
                    if (_engineManager.IsAnyStarpowerActive)
                    {
                        delta *= RockMeterPreset.StarPowerEffectMultiplier;
                    }
                }

                AddHappiness(delta);
            }

            private void OnNoteHit<TNote>(int index, TNote note) where TNote : Note<TNote>
            {
                // Ignore any notes that have not been fully hit yet on the assumption that a call
                // where the note group was fully hit will eventually come if they are all hit
                if (!note.WasFullyHit())
                {
                    return;
                }

                var delta = HAPPINESS_PER_NOTE_HIT * RockMeterPreset.HitRecoveryMultiplier;
                if (_engineManager.IsAnyStarpowerActive)
                {
                    delta *= RockMeterPreset.StarPowerEffectMultiplier;
                }

                AddHappiness(delta);
            }

            private void OnNoteMissed<TNote>(int index, TNote note) where TNote : Note<TNote>
            {
                if (!note.WasFullyMissed())
                {
                    return;
                }

                var delta = -1 * HAPPINESS_PER_NOTE_MISS * RockMeterPreset.MissDamageMultiplier;
                AddHappiness(delta);
            }

            private void OnOverstrum()
            {
                var delta = -1 * HAPPINESS_PER_NOTE_MISS * RockMeterPreset.OverhitDamageMultiplier;
                AddHappiness(delta);
            }

            private void OnKeysOverhit(int key) => OnOverstrum();

            private void OnStarpowerStatus(bool isActive) => _engineManager._starpowerCount += isActive ? 1 : -1;

            private void AddHappiness(float delta)
            {
                Happiness = Math.Clamp(Happiness + delta, HAPPINESS_MINIMUM, 1f);
                // Send over threshold event when happiness goes from below threshold to above
                if (Happiness >= HAPPINESS_CROWD_THRESHOLD && _previousHappiness < HAPPINESS_CROWD_THRESHOLD)
                {
                    OnHappinessOverThreshold?.Invoke();
                }

                // Send over threshold event when happiness goes from below threshold to above
                if (Happiness >= HAPPINESS_CROWD_THRESHOLD && _previousHappiness < HAPPINESS_CROWD_THRESHOLD)
                {
                    OnHappinessOverThreshold?.Invoke();
                }

                if (_engineManager.CheckForFail())
                {
                    OnSongFailed?.Invoke();
                    YargLogger.LogFormatDebug("Song Fail invoked after miss by player {0} with average happiness {1}", EngineId, _engineManager.Happiness);
                }

                _previousHappiness = Happiness;
            }
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

                if (Engine is BaseEngine<ProKeysNote, ProKeysEngineParameters, ProKeysStats>
                    proKeysEngine)
                {
                    var engine = (ProKeysEngine) proKeysEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarpowerStatus;
                    engine.OnOverhit += OnKeysOverhit;
                }

                if (Engine is VocalsEngine vocalsEngine)
                {
                    vocalsEngine.OnPhraseHit += OnVocalPhraseHit;
                    vocalsEngine.OnStarPowerStatus += OnStarpowerStatus;
                }
            }
        }
    }
}