using System;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int   Score { get; set; }
        public int   Combo { get; set; }
        public float Stars { get; private set; }

        private int _currentStarIndex;

        private int[] _starScoreThresholds = new int[6];
        public  int   BandMultiplier => Math.Max(_starpowerCount * 2, 1);

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier);
            }
        }

        public void PopulateStarScoreThresholds()
        {
            int playerCount = Engines.Count;

            int[] bandStarScoreCutoffs = new int[6];
            for (int i = 0; i < 6; i++)
            {
                float totalStarCutoff = 0f;
                foreach (var engineContainer in Engines)
                {
                    // This is a hack. We shouldn't recalculate
                    totalStarCutoff += engineContainer.Engine.StarScoreThresholds[i];
                }

                //TODO: May want to adjust that .265f depending on the chart's SP status (e.g. None/Yes/Unison)
                bandStarScoreCutoffs[i] = (int) Math.Floor(totalStarCutoff *
                    (1 + .265f * (playerCount - 1)));
            }

            _starScoreThresholds = bandStarScoreCutoffs;
        }

        public void UpdateStars()
        {
            // Update which star we're on
            while (_currentStarIndex < _starScoreThresholds.Length &&
                Score > _starScoreThresholds[_currentStarIndex])
            {
                _currentStarIndex++;
            }

            // Calculate current star progress
            float progress = 0f;
            if (_currentStarIndex < _starScoreThresholds.Length)
            {
                int previousPoints = _currentStarIndex > 0 ? _starScoreThresholds[_currentStarIndex - 1] : 0;
                int nextPoints = _starScoreThresholds[_currentStarIndex];
                progress = YargMath.InverseLerpF(previousPoints, nextPoints, Score);
            }

            Stars = _currentStarIndex + progress;
        }
    }
}