using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Core.Engine
{
    // Tracks and instantiates engines, handles IPC between engines, and events that affect multiple engines
    public partial class EngineManager
    {
        private int _nextEngineIndex;
        List <EngineContainer> _allEngines = new();
        Dictionary<int, EngineContainer> _allEnginesById = new();

        public class Band
        {
            public List<EngineContainer> Engines { get; private set; }
            public int Score { get; private set; }

            private Band()
            {
                Engines = new List<EngineContainer>();
                Score = 0;
            }
        }

        public class EngineContainer
        {
            public  int          EngineId      { get; }
            public  BaseEngine   Engine        { get; }
            private Instrument   Instrument    { get; }
            private SongChart    SongChart     { get; }
            public  List<Phrase> UnisonPhrases { get; }

            public enum EngineCommandType
            {
                AwardUnisonBonus,
            }

            private struct EngineCommand
            {
                public EngineCommandType CommandType;
                public double            Time;
            }

            private List<EngineCommand> _sentCommands = new();
            private int                 _commandCount => _sentCommands.Count;
            private EngineManager       _engineManager;

            public EngineContainer(BaseEngine engine, Instrument instrument, SongChart songChart, int engineId, EngineManager manager)
            {
                EngineId = engineId;
                Engine = engine;
                Instrument = instrument;
                SongChart = songChart;
                UnisonPhrases = GetUnisonPhrases(Instrument, SongChart);
                _engineManager = manager;
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
        }

        public EngineContainer Register<TEngineType>(TEngineType engine, Instrument instrument, SongChart chart)
            where TEngineType : BaseEngine
        {
            var engineContainer = new EngineContainer(engine, instrument, chart, _nextEngineIndex++, this);

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
    }
}