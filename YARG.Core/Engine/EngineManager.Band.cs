using System;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int   Score { get; set; }
        public int   Combo { get; set; }
        public float Stars { get; private set; }

        private int _currentStarIndex;

        public int[] StarScoreThresholds = new int[6];
        public  int   BandMultiplier => Math.Max(_starpowerCount * 2, 1);

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier);
            }
        }

        public int[] GetStarScoreCutoffs(int playerCount)
        {

            int[] bandStarScoreCutoffs = new int[6];
            for (int i = 0; i < 6; i++)
            {
                float totalStarCutoff = 0f;

                foreach (var engineContainer in Engines)
                {
                    totalStarCutoff += engineContainer.Engine.StarScoreThresholds[i];
                }

                //TODO: May want to adjust that .265f depending on the chart's SP status (e.g. None/Yes/Unison)
                bandStarScoreCutoffs[i] = (int) Math.Floor(totalStarCutoff *
                    (1 + .265f * (playerCount - 1)));
            }

            return bandStarScoreCutoffs;
        }

        public void UpdateStars()
        {
            // Update which star we're on
            while (_currentStarIndex < StarScoreThresholds.Length &&
                Score > StarScoreThresholds[_currentStarIndex])
            {
                _currentStarIndex++;
            }

            // Calculate current star progress
            float progress = 0f;
            if (_currentStarIndex < StarScoreThresholds.Length)
            {
                int previousPoints = _currentStarIndex > 0 ? StarScoreThresholds[_currentStarIndex - 1] : 0;
                int nextPoints = StarScoreThresholds[_currentStarIndex];
                progress = YargMath.InverseLerpF(previousPoints, nextPoints, Score);
            }

            Stars = _currentStarIndex + progress;
        }
    }
}