using System.Globalization;
using System.Linq;
using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters
    {
        public readonly HitWindowSettings HitWindow;

        public readonly int MaxMultiplier;

        public readonly double StarPowerWhammyBuffer;
        public readonly double SustainDropLeniency;

        public readonly float[] StarMultiplierThresholds;

        public double SongSpeed;

        internal BaseEngineParameters(SerializedBaseEngineParameters baseParams)
        {
            HitWindow = new HitWindowSettings();
            MaxMultiplier = baseParams.MaxMultiplier;
            StarPowerWhammyBuffer = baseParams.StarPowerWhammyBuffer;
            SustainDropLeniency = baseParams.SustainDropLeniency;
            StarMultiplierThresholds = baseParams.StarMultiplierThresholds;
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            StarPowerWhammyBuffer = spWhammyBuffer;
            SustainDropLeniency = sustainDropLeniency;
            MaxMultiplier = maxMultiplier;
            StarMultiplierThresholds = starMultiplierThresholds;
        }

        public override string ToString()
        {
            var thresholds = string.Join(", ",
                StarMultiplierThresholds.Select(i => i.ToString(CultureInfo.InvariantCulture)));

            return
                $"Hit window: ({HitWindow.MinWindow}, {HitWindow.MaxWindow})\n" +
                $"Hit window dynamic: {HitWindow.IsDynamic}\n" +
                $"Max multiplier: {MaxMultiplier}\n" +
                $"Star thresholds: {thresholds}";
        }
    }
}