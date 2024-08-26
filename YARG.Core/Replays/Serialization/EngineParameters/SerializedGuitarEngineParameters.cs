namespace YARG.Core.Replays.Serialization
{
    internal class SerializedGuitarEngineParameters
    {
        public double HopoLeniency;

        public double StrumLeniency;
        public double StrumLeniencySmall;

        public bool InfiniteFrontEnd;
        public bool AntiGhosting;

        // Removed in version 6+
        public double StarPowerWhammyBuffer;
    }
}