using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Game;

namespace YARG.Core.Engine
{
    // Tracks and instantiates engines, handles IPC between engines, and events that affect multiple engines
    public partial class EngineManager
    {
        private int                  _nextEngineIndex;
        List<EngineContainer>        _allEngines = new();
        public List<EngineContainer> Engines => _allEngines;

        private SongChart? _chart;

        public abstract partial class EngineContainer
        {
            public enum ChartStarPowerPresence
            {
                HasUnisons,
                HasStarPower,
                None
            }

            public    int                    EngineId                { get; }
            public    int                    HarmonyIndex            { get; }
            public    List<UnisonPhrase>     UnisonPhrases           { get; }
            public    ChartStarPowerPresence StarPowerPresenceStatus { get; protected set; }
            public    RockMeterPreset        RockMeterPreset         { get; }
            protected List<EngineCommand>    SentCommands = new();
            private   int                    CommandCount => SentCommands.Count;
            protected EngineManager          EngineManager;

            protected void OnStarPowerStatus(bool active)
            {
                int count = EngineManager._starpowerCount;
                count += active ? 1 : -1;
                EngineManager.UpdateStarPowerCount(count);
            }

            public virtual void UnsubscribeFromEvents()
            {
                BaseEngine.OnCodaStart -= EngineManager.CodaStartHandler;
                BaseEngine.OnCodaEnd -= EngineManager.CodaEndHandler;
            }

            public abstract void SendCommand(EngineCommandType command);
            public abstract void UpdateEngine(double time);
            public abstract BaseEngine BaseEngine { get; }
            public abstract Instrument Instrument { get; }
            public abstract Difficulty Difficulty { get; }
            public abstract void SubscribeToStarPowerPhraseHit();
            public abstract void UnsubscribeFromStarPowerPhraseHit();

            protected EngineContainer(int engineId, int harmonyIndex, EngineManager manager,
                RockMeterPreset rockMeterPreset, List<UnisonPhrase> unisonPhrases)
            {
                EngineId = engineId;
                HarmonyIndex = harmonyIndex;
                EngineManager = manager;
                RockMeterPreset = rockMeterPreset;
                UnisonPhrases = unisonPhrases;
            }
        }

        public partial class EngineContainer<TNoteType, TEngineParams, TEngineStats> : EngineContainer
            where TNoteType : Note<TNoteType>
            where TEngineParams : BaseEngineParameters
            where TEngineStats : BaseStats, new()
        {
            public BaseEngine<TNoteType, TEngineParams, TEngineStats> Engine               { get; }
            public InstrumentDifficulty<TNoteType>                    InstrumentDifficulty { get; }

            public EngineContainer(BaseEngine<TNoteType, TEngineParams, TEngineStats> engine,
                InstrumentDifficulty<TNoteType> instrumentDifficulty, int harmonyIndex, SongChart songChart,
                int engineId, EngineManager manager, RockMeterPreset rockMeterPreset)
                : base(engineId, harmonyIndex, manager, rockMeterPreset,
                    GetUnisonPhrases(instrumentDifficulty, songChart, engine is DrumsEngine))
            {
                Engine = engine;
                InstrumentDifficulty = instrumentDifficulty;
                PopulateStarScoreThresholds();
                SubscribeToEvents();
            }

            private void PopulateStarScoreThresholds()
            {
                /*
                 * These multipliers came from a rough estimation of how SP and unison phrases affect the total score of a chart.
                 * Roughly, you can expect an SP phrase every 8 measures, and every other SP phrase to be a unison phrase.
                 * SP phrases give 1/4th of a bar, and unison phrases give 1/2 of a bar of SP, and a bar lasts 8 measures.
                 * You can roughly say that every SP phrase gives 1.5 measures of SP, every ~8 measures, or a 15% increase in score.
                 * Why make it 30% then? I don't know, it seems vaguely correct.
                 */
                double multiplier;
                if (UnisonPhrases.Count > 0)
                {
                    // Has unison phrases, use the default star thresholds for unison events
                    multiplier = 1.0;
                    StarPowerPresenceStatus = ChartStarPowerPresence.HasUnisons;
                }
                else if (InstrumentDifficulty.Notes.Any(note => note.IsStarPower))
                {
                    // Has star power notes, multiply thresholds by (1.2 / 1.3)
                    multiplier = 1.2 / 1.3;
                    StarPowerPresenceStatus = ChartStarPowerPresence.HasStarPower;
                }
                else
                {
                    StarPowerPresenceStatus = ChartStarPowerPresence.None;
                    // has neither, divide thresholds by 1.3
                    if (Instrument is Instrument.PartyVocals or Instrument.Vocals or Instrument.Harmony)
                    {
                        // These have no concept of unison phrases, so we divide by 1.2 instead
                        // since the thresholds are already lower than other instruments
                        multiplier = 1.0 / 1.2;
                    }
                    else
                    {
                        multiplier = 1.0 / 1.3;
                    }
                }

                Engine.PopulateStarScoreThresholds(multiplier);
            }

            public override void SendCommand(EngineCommandType command)
            {
                // TODO: This will require rethinking when there are more commands, but for now this should work?
                if (command == EngineCommandType.AwardUnisonBonus)
                {
                    Engine.AwardUnisonBonus();
                }
                else
                {
                    return;
                }

                SentCommands.Add(new EngineCommand
                {
                    CommandType = command,
                    Time = Engine.CurrentTime,
                });
            }

            public void OnStarPowerPhraseHit(TNoteType note)
            {
                EngineManager.OnStarPowerPhraseHit(this, note.Time);
            }

            public override void UpdateEngine(double time)
            {
                Engine.Update(time);
            }

            public override BaseEngine BaseEngine => Engine;
            public override Instrument Instrument => InstrumentDifficulty.Instrument;

            public override Difficulty Difficulty => InstrumentDifficulty.Difficulty;

            public override void SubscribeToStarPowerPhraseHit()
            {
                Engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            }

            public override void UnsubscribeFromStarPowerPhraseHit()
            {
                Engine.OnStarPowerPhraseHit -= OnStarPowerPhraseHit;
            }
        }

        public EngineContainer Register<TNoteType, TEngineParams, TEngineStats>(
            BaseEngine<TNoteType, TEngineParams, TEngineStats> engine,
            InstrumentDifficulty<TNoteType> instrumentDifficulty, SongChart chart, RockMeterPreset rockMeterPreset)
            where TNoteType : Note<TNoteType>
            where TEngineParams : BaseEngineParameters
            where TEngineStats : BaseStats, new() =>
            Register(engine, instrumentDifficulty, 0, chart, rockMeterPreset);

        public EngineContainer Register<TNoteType, TEngineParams, TEngineStats>(
            BaseEngine<TNoteType, TEngineParams, TEngineStats> engine,
            InstrumentDifficulty<TNoteType> instrumentDifficulty, int harmonyIndex, SongChart chart,
            RockMeterPreset rockMeterPreset)
            where TNoteType : Note<TNoteType>
            where TEngineParams : BaseEngineParameters
            where TEngineStats : BaseStats, new()
        {
            if (_chart == null)
            {
                _chart = chart;
            }
            else
            {
                if (_chart != chart)
                {
                    throw new ArgumentException("Cannot register engine with different chart");
                }
            }

            var engineContainer = new EngineContainer<TNoteType, TEngineParams, TEngineStats>(engine,
                instrumentDifficulty, harmonyIndex, chart, _nextEngineIndex++, this, rockMeterPreset);

            // _previousHappiness = rockMeterPreset.StartingHappiness;

            _allEngines.Add(engineContainer);
            AddPlayerToUnisons(engineContainer, chart);
            engine.OnCodaStart += CodaStartHandler;
            engine.OnCodaEnd += CodaEndHandler;

            return engineContainer;
        }

        private void UpdateStarPowerCount(int count)
        {
            _starpowerCount = Math.Clamp(count, 0, int.MaxValue);
            UpdateBandMultiplier();

            if (_playerFailed && count > 0)
            {
                RevivePlayer();
            }
        }

        /// <summary>
        /// Resets mutable state - stars, codas, fail meter, band combo, unison event successes.
        /// </summary>
        public void ResetState()
        {
            _activeCodaCount = 0;
            _currentStarIndex = 0;
            _previousHappiness = 100f;
            _starpowerCount = 0;
            _activeCodaCount = 0;
            _happinessAdjustment = 0f;
            _lastUpdateTime = 0f;
            _playerFailed = false;
            // These values are derived from others, so there's no reason to reset them
            // Score = 0; derived from all players' Score + BandBonusScore
            // Stars = 0; derived from Score

            // Combo is calculated a bit differently, so we still reset it even though it's dependent on player combo
            Combo = 0;
            foreach (var engineContainer in _allEngines)
            {
                engineContainer.ResetHappiness();
            }

            foreach (var unisonEvent in _unisonEvents)
            {
                unisonEvent.Reset();
            }

            //_noFail = false; There should be no instance where we want to reset this
        }

        public void UpdateEngines(double time)
        {
            foreach (var engine in _allEngines)
            {
                engine.UpdateEngine(time);
            }
        }

        public enum EngineCommandType
        {
            AwardUnisonBonus,
        }

        public struct EngineCommand
        {
            public EngineCommandType CommandType;
            public double            Time;
        }

        public void Unregister(EngineContainer engineContainer)
        {
            RemovePlayerFromUnisons(engineContainer);
            _allEngines.Remove(engineContainer);
            engineContainer.UnsubscribeFromEvents();
            RecalculateBandState();

            if (engineContainer.BaseEngine.CodaHasStarted)
            {
                _activeCodaCount--;
            }
        }

        /// <summary>
        /// Unregisters all engines, removes all unison events, and resets all game state.
        /// </summary>
        public void Reset()
        {
            foreach (var engine in _allEngines)
            {
                engine.UnsubscribeFromEvents();
            }

            _allEngines.Clear();
            _unisonEvents.Clear();
            _nextEngineIndex = 0;
            ResetState();
        }

        private void RecalculateBandState()
        {
            int activeSpCount = 0;
            for (int i = 0; i < _allEngines.Count; i++)
            {
                var engine = _allEngines[i];
                if (engine.BaseEngine.BaseStats.IsStarPowerActive) activeSpCount++;
            }

            UpdateStarPowerCount(activeSpCount);
            UpdateBandMultiplier();
            UpdateHappiness();
        }
    }
}