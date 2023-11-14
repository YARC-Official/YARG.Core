using System.IO;

namespace YARG.Core.Engine.Logging
{
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

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);
            
            IsActive = reader.ReadBoolean();
        }
    }
}