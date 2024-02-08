using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine.Logging
{
    public class ScoreEngineEvent : BaseEngineEvent
    {
        public int Score;

        public ScoreEngineEvent(double eventTime) : base(EngineEventType.Score, eventTime)
        {
        }

        public override void Serialize(IBinaryDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Score);
        }

        public override void Deserialize(IBinaryDataReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            Score = reader.ReadInt32();
        }
    }
}