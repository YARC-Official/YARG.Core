using System;
using System.IO;
using YARG.Core.Input;

namespace YARG.Core.Engine.Logging
{
    public class InputEngineEvent : BaseEngineEvent
    {
        
        public GameInput Input;

        public InputEngineEvent(double eventTime) : base(EngineEventType.Input, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(Input.Time);
            writer.Write(Input.Action);
            writer.Write(Input.Integer);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);
            
            double time = reader.ReadDouble();
            int action = reader.ReadInt32();
            int integer = reader.ReadInt32();
            
            Input = new GameInput(time, action, integer);
        }

        public override bool Equals(BaseEngineEvent? engineEvent)
        {
            if (!base.Equals(engineEvent))
            {
                return false;
            }

            if (engineEvent?.GetType() != typeof(InputEngineEvent)) return false; 
            
            var inputEngineEvent = engineEvent as InputEngineEvent;
            
            return inputEngineEvent != null &&
                   Math.Abs(Input.Time - inputEngineEvent.Input.Time) < double.Epsilon &&
                   Input.Action == inputEngineEvent.Input.Action &&
                   Input.Integer == inputEngineEvent.Input.Integer;
        }
    }
}