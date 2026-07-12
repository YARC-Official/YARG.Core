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
        private const float HAPPINESS_FAIL_THRESHOLD        = 0.0f;

        /// <summary>
        /// The amount of happiness lost if a single note/gem for non-vocal players is hit.
        /// </summary>
        private const float HAPPINESS_PER_NOTE_HIT          = HAPPINESS_PER_NOTE_MISS / 4;

        /// <summary>
        /// The amount of happiness lost if a single note/gem for non-vocal players is completely missed.
        /// Note that this value also controls the amount of happiness lost for overstrums/overhits, but this is further scaled by the
        /// <see cref="YARG.Core.Game.RockMeterPreset.OverHitDamageMultiplier">OverHitDamageMultiplier</see> value of the current RockMeterPreset.
        /// </summary>
        private const float HAPPINESS_PER_NOTE_MISS         = 1.0f / 42;

        /// <summary>
        /// The amount of happiness gained if a vocal phrase is completed with an AWESOME rating.
        /// The exact amount gained scales depending on how far above the <see cref="VOCAL_HIT_PERC_MIDPOINT">VOCAL_HIT_PERC_MIDPOINT</see> value the player is.
        /// </summary>
        private const float HAPPINESS_PER_VOCAL_PHRASE_HIT  = 6.0f / 42;

        /// <summary>
        /// The amount of happiness lost if a vocal phrase is missed completely.
        /// The exact amount lost scales depending on how far below the <see cref="VOCAL_HIT_PERC_MIDPOINT">VOCAL_HIT_PERC_MIDPOINT</see> value the player is.
        /// </summary>
        private const float HAPPINESS_PER_VOCAL_PHRASE_MISS = 12.0f / 42;

        /// <summary>
        /// The hit percent at which no happiness is gained or lost for a vocal phrase.
        /// </summary>
        private const float VOCAL_HIT_PERC_MIDPOINT         = 0.75f;

        /// <summary>
        /// The amount of happiness required for a song's crowd stem to be enabled, if available.
        /// </summary>
        /// This is tuned to be slightly below the default starting happiness so songs with crowd cheering at
        /// the start will have the crowd stem enabled
        private const float HAPPINESS_CROWD_THRESHOLD       = 0.83f;

        /// <summary>
        /// The absolute minimum happiness value for a single player.
        /// </summary>
        private const float HAPPINESS_MINIMUM               = 0f;

        /// <summary>
        /// The amount per second happiness is lost when a player has failed.
        /// TODO: It might be better if this were based on the BPM of the song
        /// </summary>
        private const float HAPPINESS_FAIL_LOSS             = 0.05f;

        public  float Happiness => GetAverageHappiness();

        // We set this to max because the crowd stem is enabled by default and we want the first
        // update to disable the crowd stem when the rock meter preset has an initial happiness
        // below the crowd threshold
        private float _previousHappiness = 100f;

        private int   _starpowerCount = 0;

        private bool _playerFailed = false;

        private float  _happinessAdjustment = 0f;
        private double _lastUpdateTime      = 0;

        private bool _noFail;

        public        bool IsAnyStarpowerActive => _starpowerCount > 0;

        public delegate void PlayerFailed(int engineId);
        public delegate void PlayerRevived();
        public delegate void SongFailed();
        public delegate void HappinessOverThreshold();
        public delegate void HappinessUnderThreshold();

        public event PlayerFailed? OnPlayerFailed;
        public event PlayerRevived? OnPlayerRevived;
        public event SongFailed? OnSongFailed;
        public event HappinessOverThreshold? OnHappinessOverThreshold;
        public event HappinessUnderThreshold? OnHappinessUnderThreshold;

        public void InitializeHappiness(bool noFail)
        {
            _noFail = noFail;
            _playerFailed = false;
            foreach (var container in _allEngines)
            {
                container.ResetHappiness();
                container.SetNoFail(noFail);
            }

            UpdateHappiness();
        }

        public void NoFailChanged(bool noFail)
        {
            _noFail = noFail;
            foreach (var container in _allEngines)
            {
                container.SetNoFail(noFail);
            }

            if (noFail && _playerFailed)
            {
                _playerFailed = false;
                _happinessAdjustment = 0f;
            }
        }

        // Slight misnomer since all players are revived, not just one
        public void RevivePlayer()
        {
            _playerFailed = false;
            _happinessAdjustment = 0f;
            foreach (var engine in _allEngines)
            {
                engine.RevivePlayer();
            }

            OnPlayerRevived?.Invoke();
        }

        private bool UpdateHappiness()
        {
            double currentTime = _lastUpdateTime;
            // Get current engine time
            foreach (var engine in Engines)
            {
                if (engine.Engine.CurrentTime > _lastUpdateTime)
                {
                    currentTime = engine.Engine.CurrentTime;
                }
            }

            if (!_noFail)
            {
                foreach (var engine in Engines)
                {
                    if (engine.Happiness <= HAPPINESS_FAIL_THRESHOLD)
                    {
                        _playerFailed = true;
                        OnPlayerFailed?.Invoke(engine.EngineId);
                    }
                }
            }

            if (_playerFailed && !_noFail)
            {
                _happinessAdjustment += (float) (HAPPINESS_FAIL_LOSS * (currentTime - _lastUpdateTime));
            }

            _lastUpdateTime = currentTime;

            if (Happiness <= HAPPINESS_FAIL_THRESHOLD && !_noFail)
            {
                OnSongFailed?.Invoke();
                return true;
            }

            // Send over threshold event when happiness goes from below threshold to above
            if (Happiness >= HAPPINESS_CROWD_THRESHOLD && _previousHappiness < HAPPINESS_CROWD_THRESHOLD)
            {
                OnHappinessOverThreshold?.Invoke();
            }
            // Send under threshold event when happiness goes from above threshold to below
            else if (Happiness < HAPPINESS_CROWD_THRESHOLD && _previousHappiness >= HAPPINESS_CROWD_THRESHOLD)
            {
                OnHappinessUnderThreshold?.Invoke();
            }

            _previousHappiness = Happiness;

            return false;
        }

        public EngineContainer? GetLowestHappiness()
        {
            float happiness = 1.0f;
            EngineContainer? lowestHappiness = null;
            foreach (var engine in _allEngines)
            {
                if (engine.Happiness < happiness)
                {
                    happiness = engine.Happiness;
                    lowestHappiness = engine;
                }
            }

            return lowestHappiness;
        }

        private float GetAverageHappiness()
        {
            float happiness = 0.0f;
            foreach (var engine in _allEngines)
            {
                happiness += engine.Happiness;
            }

            return (happiness / _allEngines.Count) - _happinessAdjustment;
        }

        public partial class EngineContainer
        {
            private const float HAPPINESS_NEAR_FAIL_THRESHOLD = 0.333f;
            public        float Happiness { get; private set; } = 0.0f;
            private       bool  _nearFail         = false;
            private       bool  _thisPlayerFailed = false;

            private bool _noFail;

            // Delegates and events
            public delegate void HappinessNearFailEvent();
            public delegate void HappinessOverFailEvent();

            public event HappinessNearFailEvent? OnHappinessNearFail;
            public event HappinessOverFailEvent? OnHappinessOverFail;

            public void SetNoFail(bool noFail)
            {
                _noFail = noFail;
                if (noFail && _thisPlayerFailed)
                {
                    RevivePlayer();
                }
            }

            public void ResetHappiness()
            {
                _thisPlayerFailed = false;
                Happiness = RockMeterPreset.StartingHappiness;
            }

            private void OnVocalPhraseHit(double hitPercentAfterParams, bool fullPoints, bool isLastPhrase)
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

            public void RevivePlayer()
            {
                if (_thisPlayerFailed)
                {
                    Happiness = 0.5f;
                }

                Engine.PlayerHasRevived();

                if (!_nearFail)
                {
                    OnHappinessOverFail?.Invoke();
                }

                _thisPlayerFailed = false;
            }

            public void PlayerHasFailed(int engineId)
            {
                if (_noFail)
                {
                    return;
                }

                if (engineId == EngineId)
                {
                    _thisPlayerFailed = true;
                }

                Engine.PlayerHasFailed();
                OnHappinessNearFail?.Invoke();
            }

            private void AddHappiness(float delta)
            {
                Happiness = Math.Clamp(Happiness + delta, HAPPINESS_MINIMUM, 1f);
                if (Happiness < HAPPINESS_NEAR_FAIL_THRESHOLD && !_nearFail)
                {
                    _nearFail = true;
                    if (!_noFail)
                    {
                        OnHappinessNearFail?.Invoke();
                    }
                }
                else if (Happiness >= HAPPINESS_NEAR_FAIL_THRESHOLD && _nearFail)
                {
                    _nearFail = false;
                    // This is not gated behind _noFail being false because we want to always allow transitions
                    // away from fail in case no fail has been turned on
                    OnHappinessOverFail?.Invoke();
                }

                _engineManager.UpdateHappiness();
            }

            private void OnPlayerFailedReceived(int engineId)
            {
                PlayerHasFailed(engineId);
            }

            private void OnPlayerRevivedReceived()
            {
                _engineManager.RevivePlayer();
            }

            private void SubscribeToEvents()
            {
                // Subscribe to OnNoteHit and OnNoteMissed events
                if (Engine is BaseEngine<GuitarNote,GuitarEngineParameters,GuitarStats> guitarEngine)
                {
                    var engine = (GuitarEngine) guitarEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarPowerStatus;
                    engine.OnOverstrum += OnOverstrum;
                }

                if (Engine is BaseEngine<DrumNote, DrumsEngineParameters, DrumsStats> drumEngine)
                {
                    var engine = (DrumsEngine) drumEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarPowerStatus;
                    engine.OnOverhit += OnOverstrum;
                }

                if (Engine is BaseEngine<ProKeysNote, KeysEngineParameters, KeysStats>
                    proKeysEngine)
                {
                    var engine = (ProKeysEngine) proKeysEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarPowerStatus;
                    engine.OnOverhit += OnKeysOverhit;
                }

                if (Engine is BaseEngine<GuitarNote, KeysEngineParameters, KeysStats> keysEngine)
                {
                    var engine = (FiveLaneKeysEngine) keysEngine;
                    engine.OnNoteHit += OnNoteHit;
                    engine.OnNoteMissed += OnNoteMissed;
                    engine.OnStarPowerStatus += OnStarPowerStatus;
                    engine.OnOverhit += OnKeysOverhit;
                }

                if (Engine is VocalsEngine vocalsEngine)
                {
                    vocalsEngine.OnPhraseHit += OnVocalPhraseHit;
                    vocalsEngine.OnStarPowerStatus += OnStarPowerStatus;
                }

                _engineManager.OnPlayerFailed += OnPlayerFailedReceived;
                Engine.OnPlayerRevived += OnPlayerRevivedReceived;
            }
        }
    }
}