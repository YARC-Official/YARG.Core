using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Logging;

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

            public EngineContainer(BaseEngine engine, Instrument instrument, SongChart songChart, int engineId)
            {
                EngineId = engineId;
                Engine = engine;
                Instrument = instrument;
                SongChart = songChart;
                UnisonPhrases = GetUnisonPhrases(Instrument, SongChart);
            }
        }

        public EngineContainer Register<TEngineType>(TEngineType engine, Instrument instrument, SongChart chart)
            where TEngineType : BaseEngine
        {
            var engineContainer = new EngineContainer(engine, instrument, chart, _nextEngineIndex++);

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
    }
}