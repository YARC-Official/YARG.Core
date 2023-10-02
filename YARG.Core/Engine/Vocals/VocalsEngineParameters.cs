namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineParameters : BaseEngineParameters
    {
        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(double hitWindow, float[] starMultiplierThresholds)
            : base(hitWindow, 1f, starMultiplierThresholds)
        {
        }
    }
}