namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineParameters : BaseEngineParameters
    {
        public ProKeysEngineParameters()
        {
        }

        public ProKeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds)
            : base(hitWindow, maxMultiplier, starMultiplierThresholds)
        {
        }
    }
}