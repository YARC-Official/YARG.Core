using System.IO;

namespace YARG.Core.Engine.Logging
{
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
            writer.Write(NoteTime);
            writer.Write(NoteIndex);
            writer.Write(NoteMask);
            writer.Write(NoteLength);
            writer.Write(WasHit);
            writer.Write(WasSkipped);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);
            
            NoteTime = reader.ReadDouble();
            NoteIndex = reader.ReadInt32();
            NoteMask = reader.ReadInt32();
            NoteLength = reader.ReadInt32();
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
}