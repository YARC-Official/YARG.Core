using System;
using System.IO;

namespace YARG.Core.Engine.Logging
{
    public class TimerEngineEvent : BaseEngineEvent
    {
        public string TimerName = string.Empty;

        public double TimerValue;

        public bool TimerStarted;
        public bool TimerStopped;
        public bool TimerExpired;

        public TimerEngineEvent(double eventTime) : base(EngineEventType.Timer, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(TimerName);
            writer.Write(TimerValue);
            writer.Write(TimerStarted);
            writer.Write(TimerStopped);
            writer.Write(TimerExpired);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            TimerName = reader.ReadString();
            TimerValue = reader.ReadDouble();
            TimerStarted = reader.ReadBoolean();
            TimerStopped = reader.ReadBoolean();
            TimerExpired = reader.ReadBoolean();
        }

        public override bool Equals(BaseEngineEvent? engineEvent)
        {
            if (!base.Equals(engineEvent))
            {
                return false;
            }

            if (engineEvent?.GetType() != typeof(TimerEngineEvent)) return false;

            var timerEvent = engineEvent as TimerEngineEvent;

            return timerEvent != null &&
                TimerName == timerEvent.TimerName &&
                Math.Abs(TimerValue - timerEvent.TimerValue) < double.Epsilon &&
                TimerStarted == timerEvent.TimerStarted &&
                TimerStopped == timerEvent.TimerStopped &&
                TimerExpired == timerEvent.TimerExpired;
        }
    }
}