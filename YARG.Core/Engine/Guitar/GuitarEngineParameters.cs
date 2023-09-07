using System.IO;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {
        public double HopoLeniency { get; private set; }

        public double StrumLeniency      { get; private set; }
        public double StrumLeniencySmall { get; private set; }

        public bool InfiniteFrontEnd { get; private set; }
        public bool AntiGhosting     { get; private set; }

        public GuitarEngineParameters()
        {
        }

        public GuitarEngineParameters(double hitWindow, double frontBackRatio, float[] starMultiplierThresholds, double hopoLeniency,
            double strumLeniency, double strumLeniencySmall, bool infiniteFrontEnd, bool antiGhosting)
            : base(hitWindow, frontBackRatio, starMultiplierThresholds)
        {
            HopoLeniency = hopoLeniency;

            StrumLeniency = strumLeniency;
            StrumLeniencySmall = strumLeniencySmall;

            InfiniteFrontEnd = infiniteFrontEnd;
            AntiGhosting = antiGhosting;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(HopoLeniency);
            writer.Write(StrumLeniency);
            writer.Write(StrumLeniencySmall);
            writer.Write(InfiniteFrontEnd);
            writer.Write(AntiGhosting);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            HopoLeniency = reader.ReadDouble();
            StrumLeniency = reader.ReadDouble();
            StrumLeniencySmall = reader.ReadDouble();
            InfiniteFrontEnd = reader.ReadBoolean();
            AntiGhosting = reader.ReadBoolean();
        }
    }
}