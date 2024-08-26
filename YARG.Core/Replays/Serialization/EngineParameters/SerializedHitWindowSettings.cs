namespace YARG.Core.Replays.Serialization
{
    internal class SerializedHitWindowSettings
    {
        public double MaxWindow;
        public double MinWindow;

        public bool IsDynamic;

        public double DynamicWindowSlope;
        public double DynamicWindowScale;
        public double DynamicWindowGamma;
        public double FrontToBackRatio;
    }
}