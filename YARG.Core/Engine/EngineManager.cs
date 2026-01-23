using System;
using System.Collections.Generic;
using YARG.Core.Chart;
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


        public partial class EngineContainer
        {
            public  int             EngineId         { get; }
            public  BaseEngine      Engine           { get; }
            public  Instrument      Instrument       { get; }
            public  int             HarmonyIndex     { get; }
            private SongChart       SongChart        { get; }
            public  List<Phrase>    UnisonPhrases    { get; }
            public  RockMeterPreset RockMeterPreset  { get; }

            private List<EngineCommand> _sentCommands = new();
            private int                 _commandCount => _sentCommands.Count;
            private EngineManager       _engineManager;

            public EngineContainer(BaseEngine engine, Instrument instrument, int harmonyIndex, SongChart songChart, int engineId, EngineManager manager, RockMeterPreset rockMeterPreset)
            {
                EngineId = engineId;
                Engine = engine;
                Instrument = instrument;
                HarmonyIndex = harmonyIndex;
                SongChart = songChart;
                UnisonPhrases = GetUnisonPhrases(Instrument, SongChart);
                RockMeterPreset = rockMeterPreset;
                _engineManager = manager;
                Happiness = rockMeterPreset.StartingHappiness;

                SubscribeToEngineEvents();
            }

            public void SendCommand(EngineCommandType command)
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
                _sentCommands.Add(new EngineCommand { CommandType = command, Time = Engine.CurrentTime });
            }

            public void OnStarPowerPhraseHit<TNote>(TNote note) where TNote : Note<TNote>
            {
                _engineManager.OnStarPowerPhraseHit(this, note.Time);
            }

            public void UpdateEngine(double time)
            {
                Engine.Update(time);
            }

            private void OnStarPowerStatus(bool active)
            {
                int delta = active ? 1 : -1;
                if (!Engine.IsBot)
                {
                    _engineManager._humanStarpowerCount += delta;
                }
                _engineManager._starpowerCount += delta;
                _engineManager.UpdateBandMultiplier();
            }
        }

        public EngineContainer Register<TEngineType>(TEngineType engine, Instrument instrument, SongChart chart, RockMeterPreset rockMeterPreset)
            where TEngineType : BaseEngine
        {
            return Register(engine, instrument, 0, chart, rockMeterPreset);
        }

        public EngineContainer Register<TEngineType>(TEngineType engine, Instrument instrument, int harmonyIndex, SongChart chart, RockMeterPreset rockMeterPreset)
            where TEngineType : BaseEngine
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

            var engineContainer = new EngineContainer(engine, instrument, harmonyIndex, chart, _nextEngineIndex++, this, rockMeterPreset);

            // _previousHappiness = rockMeterPreset.StartingHappiness;

            _allEngines.Add(engineContainer);
            _allEnginesById.Add(engineContainer.EngineId, engineContainer);
            AddPlayerToUnisons(engineContainer);

            return engineContainer;
        }

        private EngineContainer GetEngineContainer(BaseEngine target)
        {
            foreach (var engine in _allEngines)
            {
                if (engine.Engine == target)
                {
                    return engine;
                }
            }
            throw new ArgumentException("Target engine not found");
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

        private struct EngineCommand
        {
            public EngineCommandType CommandType;
            public double            Time;
        }
    }
}