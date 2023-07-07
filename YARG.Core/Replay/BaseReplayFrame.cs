using System;
using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Core.Replay
{

    public abstract class BaseReplayFrame
    {
        public int        PlayerId;
        public string     PlayerName;
        public Instrument Instrument;
        public Difficulty Difficulty;
    }

    public class ReplayFrame<TStats, TAction> : BaseReplayFrame
        where TStats : BaseStats
        where TAction : unmanaged, Enum
    {
        public TStats     Stats;
        public int        InputCount;
        public GameInput[]   Inputs;
    }
}