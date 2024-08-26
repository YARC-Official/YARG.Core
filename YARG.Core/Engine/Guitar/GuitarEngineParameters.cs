using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {
        public readonly double HopoLeniency;

        public readonly double StrumLeniency;
        public readonly double StrumLeniencySmall;

        public readonly bool InfiniteFrontEnd;
        public readonly bool AntiGhosting;

        internal GuitarEngineParameters(SerializedGuitarEngineParameters guitarParams,
            SerializedBaseEngineParameters baseParams) : base(baseParams)
        {
            HopoLeniency = guitarParams.HopoLeniency;

            StrumLeniency = guitarParams.StrumLeniency;
            StrumLeniencySmall = guitarParams.StrumLeniencySmall;

            InfiniteFrontEnd = guitarParams.InfiniteFrontEnd;
            AntiGhosting = guitarParams.AntiGhosting;
        }

        public GuitarEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double hopoLeniency, double strumLeniency,
            double strumLeniencySmall, bool infiniteFrontEnd, bool antiGhosting)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            HopoLeniency = hopoLeniency;

            StrumLeniency = strumLeniency;
            StrumLeniencySmall = strumLeniencySmall;

            InfiniteFrontEnd = infiniteFrontEnd;
            AntiGhosting = antiGhosting;
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