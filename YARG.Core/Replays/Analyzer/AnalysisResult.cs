using System.Collections.Generic;
using YARG.Core.Engine;
using YARG.Core.Engine.Logging;

namespace YARG.Core.Replays.Analyzer
{
    public struct AnalysisResult
    {
        public bool Passed;

        public BaseStats Stats;

        public int ScoreDifference;

        public EngineEventLogger? EventLogger;
    }
}