using System.IO;

namespace YARG.Core.Engine.Logging
{
    public class ScoreEngineEvent : BaseEngineEvent
    {
        
        public int Score;

        public ScoreEngineEvent(double eventTime) : base(EngineEventType.Score, eventTime)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(Score);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);
            
            Score = reader.ReadInt32();
        }
    }
}