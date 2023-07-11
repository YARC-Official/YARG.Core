using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Core.Replays
{
    public abstract class ReplayFrame
    {
        public int        PlayerId;
        public string     PlayerName;
        public Instrument Instrument;
        public Difficulty Difficulty;
    }

    public class ReplayFrame<TStats> : ReplayFrame where TStats : BaseStats
    {
        public TStats      Stats;
        public int         InputCount;
        public GameInput[] Inputs;
    }
}