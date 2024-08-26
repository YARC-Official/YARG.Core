using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Core.Replays
{
    public class ReplayFrame
    {
        public ReplayPlayerInfo     PlayerInfo;
        public BaseEngineParameters EngineParameters;
        public BaseStats            Stats;
        public int                  InputCount;
        public GameInput[]          Inputs;

        public ReplayFrame(ReplayPlayerInfo playerInfo, BaseEngineParameters engineParams, BaseStats stats, GameInput[] inputs)
        {
            PlayerInfo = playerInfo;
            EngineParameters = engineParams;
            Stats = stats;
            Inputs = inputs;
            InputCount = inputs.Length;
        }
    }
}