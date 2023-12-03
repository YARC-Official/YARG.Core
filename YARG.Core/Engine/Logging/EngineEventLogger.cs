using System.Collections.Generic;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine.Logging
{
    public class EngineEventLogger : IBinarySerializable
    {
        public IReadOnlyList<BaseEngineEvent> Events => _events;

        private readonly List<BaseEngineEvent> _events = new();

        public void LogEvent(BaseEngineEvent engineEvent)
        {
            _events.Add(engineEvent);
        }

        public void Clear()
        {
            _events.Clear();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(_events.Count);
            foreach (var engineEvent in _events)
            {
                engineEvent.Serialize(writer);
            }
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var engineEvent = GetEventObjectFromType((EngineEventType) reader.ReadInt32());

                if (engineEvent is null) break;

                engineEvent.Deserialize(reader, version);

                _events.Add(engineEvent);
            }
        }

        private static BaseEngineEvent? GetEventObjectFromType(EngineEventType type)
        {
            return type switch
            {
                EngineEventType.Note      => new NoteEngineEvent(0),
                //EngineEventType.Sustain => new SustainEngineEvent(type, 0),
                EngineEventType.Timer     => new TimerEngineEvent(0),
                EngineEventType.Score     => new ScoreEngineEvent(0),
                EngineEventType.StarPower => new StarPowerEngineEvent(0),
                _                         => null
            };
        }
    }
}