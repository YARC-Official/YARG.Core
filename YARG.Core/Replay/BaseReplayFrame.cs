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

    public class ReplayFrame<TStats, TAction, TInput> : BaseReplayFrame
        where TStats : BaseStats
        where TAction : unmanaged, Enum
        where TInput : GameInput
    {
        public TStats     Stats;
        public int        InputCount;
        public TInput[]   Inputs;
    }
}