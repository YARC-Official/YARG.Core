namespace YARG.Core.Engine.Drums
{
    public class DrumsEngineParameters : BaseEngineParameters
    {
        public DrumsEngineParameters()
        {
        }

        public DrumsEngineParameters(double hitWindow, double frontBackRatio, float[] starMultiplierThresholds)
            : base(hitWindow, frontBackRatio, starMultiplierThresholds)
        {
        }
    }
}