using System.Collections.Generic;
using YARG.Core.Engine;

namespace YARG.Core.Replays.Analyzer
{
    public struct AnalysisResult
    {

        public bool Passed;

        public BaseStats Stats;

        public int ScoreDifference;

        public List<int> NoteHitDifferences;
        public List<int> NoteMissDifferences;

    }
}