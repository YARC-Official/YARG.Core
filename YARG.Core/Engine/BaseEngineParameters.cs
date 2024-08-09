using System;
using System.Globalization;
using System.Linq;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters
    {
        public HitWindowSettings HitWindow;

        public int MaxMultiplier;

        public float[] StarMultiplierThresholds;

        public double StarPowerWhammyBuffer;
        public double SongSpeed;

        protected BaseEngineParameters()
        {
            HitWindow = new HitWindowSettings();
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            StarPowerWhammyBuffer = spWhammyBuffer;
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