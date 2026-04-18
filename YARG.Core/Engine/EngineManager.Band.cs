using System;
using System.Collections.Generic;

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

        private int          _activeCodaCount;

        public delegate void CodaStartDelegate(CodaSection codaSection);
        public delegate void CodaEndDelegate(CodaSection codaSection);

        public event CodaStartDelegate? OnCodaStart;
        public event CodaEndDelegate? OnCodaEnd;

        public int TotalCodaBonus
        {
            get
            {
                var totalBonus = 0;
                foreach (var engine in Engines)
                {
                    totalBonus += engine.Engine.CurrentCodaBonus;
                }

                return totalBonus;
            }
        }

        public bool CodaSuccess
        {
            get
            {
                foreach (var engine in Engines)
                {
                    if (!engine.Engine.CodaSuccess)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void CodaStartHandler(CodaSection coda)
        {
            if (_activeCodaCount == 0)
            {
                OnCodaStart?.Invoke(coda);
            }

            _activeCodaCount++;
        }

        private void CodaEndHandler(CodaSection coda)
        {
            var success = CodaSuccess;
            _activeCodaCount--;
            if (_activeCodaCount == 0)
            {
                OnCodaEnd?.Invoke(coda);

                foreach (var engine in Engines)
                {
                    engine.Engine.AwardCodaBonus(success);
                }
            }
        }

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier);
            }
        }

        public static int[] GetStarScoreCutoffs(List<int[]> starScoreCutoffsList)
        {

            int[] bandStarScoreCutoffs = new int[6];
            for (int i = 0; i < 6; i++)
            {
                int totalStarCutoff = 0;
                foreach (var playerCutoffsList in starScoreCutoffsList)
                {
                    totalStarCutoff += playerCutoffsList[i];
                }

                bandStarScoreCutoffs[i] = (int) Math.Floor(totalStarCutoff *
                    (1 + .265f * (starScoreCutoffsList.Count - 1)));
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