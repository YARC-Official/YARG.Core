using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Core.Replays
{
    public abstract class ReplayFrame
    {
        public int         PlayerId;
        public string      PlayerName;
        public Instrument  Instrument;
        public Difficulty  Difficulty;
        public int         InputCount;
        public GameInput[] Inputs;
    }

    public class ReplayFrame<TStats> : ReplayFrame where TStats : BaseStats
    {
        public TStats Stats;
    }
}