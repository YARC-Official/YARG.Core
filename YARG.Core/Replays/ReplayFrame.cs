using System;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
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

        // Disabling this because it looks ugly with the initializers lol
        // ReSharper disable once ConvertConstructorToMemberInitializers
        public ReplayFrame()
        {
            EngineParameters = new GuitarEngineParameters();
            Stats = new GuitarStats();
            Inputs = Array.Empty<GameInput>();
        }
    }
}