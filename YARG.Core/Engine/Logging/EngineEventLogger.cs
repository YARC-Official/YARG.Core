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
                
                if(engineEvent is null)
                    break;
                
                engineEvent.Deserialize(reader, version);
                
                _events.Add(engineEvent);
            }
        }

        private static BaseEngineEvent? GetEventObjectFromType(EngineEventType type)
        {
            switch (type)
            {
                case EngineEventType.Note:
                    return new NoteEngineEvent(0);
                // case EngineEventType.Sustain:
                //     return new SustainEngineEvent(type, 0);
                case EngineEventType.Timer:
                    return new TimerEngineEvent(0);
                case EngineEventType.Score:
                    return new ScoreEngineEvent(0);
                case EngineEventType.StarPower:
                    return new StarPowerEngineEvent(0);
                default:
                    return null;
            }
        }
    }
}