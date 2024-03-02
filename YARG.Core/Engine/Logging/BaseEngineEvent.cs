using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine.Logging
{
    public abstract class BaseEngineEvent : IBinarySerializable
    {
        public EngineEventType EventType { get; }

        public double EventTime { get; private set; }

        protected BaseEngineEvent(EngineEventType eventType, double eventTime)
        {
            EventType = eventType;
            EventTime = eventTime;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((int) EventType);
            writer.Write(EventTime);
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            // Don't deserialize event type as it's done manually to determine object type

            EventTime = reader.ReadDouble();
        }

        public static bool operator ==(BaseEngineEvent? a, BaseEngineEvent? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null) return false;

            return a.Equals(b);
        }

        public static bool operator !=(BaseEngineEvent? a, BaseEngineEvent? b)
        {
            return !(a == b);
        }

        public virtual bool Equals(BaseEngineEvent? engineEvent)
        {
            if (engineEvent is null)
            {
                return false;
            }

            return Math.Abs(EventTime - engineEvent.EventTime) < double.Epsilon;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BaseEngineEvent);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EventType, EventTime);
        }
    }

    public enum EngineEventType
    {
        Note,
        Sustain,
        Input,
        Timer,
        Score,
        StarPower,
        Consistent
    }
}