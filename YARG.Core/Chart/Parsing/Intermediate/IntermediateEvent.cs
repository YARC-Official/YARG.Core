namespace YARG.Core.Chart.Parsing
{
    internal class IntermediateEvent
    {
        public uint Tick;
        public uint TickLength;
        public uint TickEnd => Tick + TickLength;

        public IntermediateEvent(uint tick, uint length)
        {
            Tick = tick;
            TickLength = length;
        }
    }
}