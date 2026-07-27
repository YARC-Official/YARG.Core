using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Game;

namespace YARG.Core.Engine
{
    // Tracks and instantiates engines, handles IPC between engines, and events that affect multiple engines
    public partial class EngineManager
    {
        private int                      _nextEngineIndex;
        List <EngineContainer>           _allEngines     = new();
        Dictionary<int, EngineContainer> _allEnginesById = new();

        public List<EngineContainer> Engines => _allEngines;

        private SongChart?               _chart;

        public abstract partial class EngineContainer
        {
            public    int                 EngineId        { get; }
            public    int                 HarmonyIndex    { get; }
            public    List<UnisonPhrase>  UnisonPhrases   { get; }
            public    RockMeterPreset     RockMeterPreset { get; }
            protected List<EngineCommand> SentCommands = new();
            private   int                 CommandCount => SentCommands.Count;
            protected EngineManager       EngineManager;

            protected void OnStarPowerStatus(bool active)
            {
                int count = EngineManager._starpowerCount;
                count += active ? 1 : -1;
                EngineManager.UpdateStarPowerCount(count);
            }

            public abstract void SendCommand(EngineCommandType command);
            public abstract void UpdateEngine(double time);
            public abstract BaseEngine BaseEngine { get; }
            public abstract Instrument Instrument { get; }
            public abstract Difficulty Difficulty { get; }
            public abstract void SubscribeToStarPowerPhraseHit();
            public abstract void UnsubscribeToStarPowerPhraseHit();

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

                SubscribeToEvents();
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

            public override void UnsubscribeToStarPowerPhraseHit()
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
            _allEnginesById.Add(engineContainer.EngineId, engineContainer);
            AddPlayerToUnisons(engineContainer, chart);
            engine.OnCodaStart += CodaStartHandler;
            engine.OnCodaEnd += CodaEndHandler;

            return engineContainer;
        }

        private EngineContainer GetEngineContainer(BaseEngine target)
        {
            foreach (var engine in _allEngines)
            {
                if (engine.BaseEngine == target)
                {
                    return engine;
                }
            }
            throw new ArgumentException("Target engine not found");
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

        public void Reset()
        {
            _activeCodaCount = 0;
            _currentStarIndex = 0;
            _previousHappiness = 100f;
            _starpowerCount = 0;
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
            _allEnginesById.Remove(engineContainer.EngineId);
        }
    }
}