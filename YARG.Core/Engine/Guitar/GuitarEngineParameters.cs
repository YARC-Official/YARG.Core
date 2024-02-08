using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {
        public double HopoLeniency { get; private set; }

        public double StrumLeniency      { get; private set; }
        public double StrumLeniencySmall { get; private set; }

        public double StarPowerWhammyBuffer { get; private set; }

        public bool InfiniteFrontEnd { get; private set; }
        public bool AntiGhosting     { get; private set; }

        public GuitarEngineParameters()
        {
        }

        public GuitarEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            double hopoLeniency, double strumLeniency, double strumLeniencySmall, double spWhammyBuffer,
            bool infiniteFrontEnd, bool antiGhosting)
            : base(hitWindow, maxMultiplier, starMultiplierThresholds)
        {
            HopoLeniency = hopoLeniency;

            StrumLeniency = strumLeniency;
            StrumLeniencySmall = strumLeniencySmall;

            StarPowerWhammyBuffer = spWhammyBuffer;

            InfiniteFrontEnd = infiniteFrontEnd;
            AntiGhosting = antiGhosting;
        }

        public override void Serialize(IBinaryDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(HopoLeniency);

            writer.Write(StrumLeniency);
            writer.Write(StrumLeniencySmall);

            writer.Write(StarPowerWhammyBuffer);

            writer.Write(InfiniteFrontEnd);
            writer.Write(AntiGhosting);
        }

        public override void Deserialize(IBinaryDataReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            HopoLeniency = reader.ReadDouble();

            StrumLeniency = reader.ReadDouble();
            StrumLeniencySmall = reader.ReadDouble();

            StarPowerWhammyBuffer = reader.ReadDouble();

            InfiniteFrontEnd = reader.ReadBoolean();
            AntiGhosting = reader.ReadBoolean();
        }
    }
}