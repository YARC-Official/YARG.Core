using System.IO;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {
        public double HopoLeniency;

        public double StrumLeniency;
        public double StrumLeniencySmall;

        public double StarPowerWhammyBuffer;

        public bool InfiniteFrontEnd;
        public bool AntiGhosting;

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

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(HopoLeniency);

            writer.Write(StrumLeniency);
            writer.Write(StrumLeniencySmall);

            writer.Write(StarPowerWhammyBuffer);

            writer.Write(InfiniteFrontEnd);
            writer.Write(AntiGhosting);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            HopoLeniency = reader.ReadDouble();

            StrumLeniency = reader.ReadDouble();
            StrumLeniencySmall = reader.ReadDouble();

            StarPowerWhammyBuffer = reader.ReadDouble();

            InfiniteFrontEnd = reader.ReadBoolean();
            AntiGhosting = reader.ReadBoolean();
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Infinite front-end: {InfiniteFrontEnd}\n" +
                $"Anti-ghosting: {AntiGhosting}\n" +
                $"Hopo leniency: {HopoLeniency}\n" +
                $"Strum leniency: {StrumLeniency}\n" +
                $"Strum leniency (small): {StrumLeniencySmall}\n" +
                $"Star power whammy buffer: {StarPowerWhammyBuffer}";
        }
    }
}