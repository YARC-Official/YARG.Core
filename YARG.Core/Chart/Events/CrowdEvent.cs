using System;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    public class CrowdEvent : ChartEvent, ICloneable<CrowdEvent>
    {
        public CrowdEventType Type { get; }
        public CrowdState CrowdState { get; }
        public ClapState ClapState { get; }

        public CrowdEvent(CrowdEventType type, CrowdState state, ClapState clap, double time, double timeLength,
            uint tick, uint tickLength) : base(time, timeLength, tick, tickLength)
        {
            Type = type;
            CrowdState = state;
            ClapState = clap;
        }

        public CrowdEvent(CrowdEvent other) : base(other)
        {
            Type = other.Type;
            CrowdState = other.CrowdState;
            ClapState = other.ClapState;
        }

        public CrowdEvent Clone()
        {
            return new CrowdEvent(this);
        }

        public enum CrowdEventType
        {
            State,
            Clap
        }
    }
}