using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Utility;

/*
 *
 * This file should NEVER be added to. It is a preservation of the old Engine Logging feature stored in replays
 * in order to correctly read v0.12 replay files. It is no longer in use and should not be used in any new code.
 *
 */

namespace YARG.Core.Replays
{
    public class EngineEventLogger
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

        public void Deserialize(ref SpanBinaryReader reader, int version = 0)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var engineEvent = GetEventObjectFromType((EngineEventType) reader.ReadInt32());

                if (engineEvent is null) break;

                engineEvent.Deserialize(ref reader, version);

                _events.Add(engineEvent);
            }
        }

        private static BaseEngineEvent? GetEventObjectFromType(EngineEventType type)
        {
            return type switch
            {
                EngineEventType.Note       => new NoteEngineEvent(0),
                //EngineEventType.Sustain  => new SustainEngineEvent(type, 0),
                EngineEventType.Timer      => new TimerEngineEvent(0),
                EngineEventType.Score      => new ScoreEngineEvent(0),
                EngineEventType.StarPower  => new StarPowerEngineEvent(0),
                EngineEventType.Consistent => new ConsistentEngineEvent(0),
                _                          => null
            };
        }
    }

    public abstract class BaseEngineEvent
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

        public virtual void Deserialize(ref SpanBinaryReader reader, int version = 0)
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

    public class ConsistentEngineEvent : BaseEngineEvent
    {
        public string Message = string.Empty;

        public ConsistentEngineEvent(double eventTime) : base(EngineEventType.Consistent, eventTime)
        {
        }

        public override bool Equals(BaseEngineEvent? engineEvent)
        {
            if (!base.Equals(engineEvent))
            {
                return false;
            }

            if (engineEvent?.GetType() != typeof(ConsistentEngineEvent)) return false;

            var consistentEngineEvent = engineEvent as ConsistentEngineEvent;

            return consistentEngineEvent?.Message == Message;
        }
    }

    public class NoteEngineEvent : BaseEngineEvent
    {
        public double NoteTime;
        public double NoteLength;

        public int NoteIndex;
        public int NoteMask;

        public bool WasHit;
        public bool WasSkipped;

        public NoteEngineEvent(double eventTime) : base(EngineEventType.Note, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(NoteTime);
            writer.Write(NoteLength);

            writer.Write(NoteIndex);
            writer.Write(NoteMask);

            writer.Write(WasHit);
            writer.Write(WasSkipped);
        }

        public override void Deserialize(ref SpanBinaryReader reader, int version = 0)
        {
            base.Deserialize(ref reader, version);

            NoteTime = reader.ReadDouble();
            NoteLength = reader.ReadDouble();

            NoteIndex = reader.ReadInt32();
            NoteMask = reader.ReadInt32();

            WasHit = reader.ReadBoolean();
            WasSkipped = reader.ReadBoolean();
        }

        public override bool Equals(BaseEngineEvent? engineEvent)
        {
            if (!base.Equals(engineEvent))
            {
                return false;
            }

            if (engineEvent?.GetType() != typeof(NoteEngineEvent)) return false;

            var noteStateEvent = engineEvent as NoteEngineEvent;

            return noteStateEvent != null &&
                NoteIndex == noteStateEvent.NoteIndex &&
                NoteMask == noteStateEvent.NoteMask &&
                NoteLength == noteStateEvent.NoteLength &&
                WasHit == noteStateEvent.WasHit &&
                WasSkipped == noteStateEvent.WasSkipped;
        }
    }

    public class ScoreEngineEvent : BaseEngineEvent
    {
        public int Score;

        public ScoreEngineEvent(double eventTime) : base(EngineEventType.Score, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Score);
        }

        public override void Deserialize(ref SpanBinaryReader reader, int version = 0)
        {
            base.Deserialize(ref reader, version);

            Score = reader.ReadInt32();
        }
    }

    public class StarPowerEngineEvent : BaseEngineEvent
    {
        public bool IsActive;

        public StarPowerEngineEvent(double eventTime) : base(EngineEventType.StarPower, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(IsActive);
        }

        public override void Deserialize(ref SpanBinaryReader reader, int version = 0)
        {
            base.Deserialize(ref reader, version);

            IsActive = reader.ReadBoolean();
        }
    }

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

        public override void Deserialize(ref SpanBinaryReader reader, int version = 0)
        {
            base.Deserialize(ref reader, version);

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