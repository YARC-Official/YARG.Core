using System;
using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        private const int NUMBER_OF_STAR_SCORE_THRESHOLDS = 6;
        public int   Score { get; set; }
        public int   Combo { get; set; }
        public float Stars { get; private set; }

        private int _currentStarIndex;

        public int[] StarScoreThresholds = new int[NUMBER_OF_STAR_SCORE_THRESHOLDS];
        public int   BandMultiplier => Math.Max(_starpowerCount * 2, 1);

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
                    totalBonus += engine.BaseEngine.CurrentCodaBonus;
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
                    if (!engine.BaseEngine.CodaSuccess)
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
                    engine.BaseEngine.AwardCodaBonus(success);
                }
            }
        }

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.BaseEngine.UpdateBandMultiplier(BandMultiplier);
            }
        }

        public static int[] GetStarScoreCutoffs(List<EngineContainer> engineContainers)
        {
#if UNITY_EDITOR || YARG_TEST_BUILD || YARG_NIGHTLY_BUILD
            int[] debugSpPresenceCounters = new int[3];
            foreach (var engineContainer in engineContainers)
            {
                YargLogger.AssertFormat(
                    engineContainer.BaseEngine.StarScoreThresholds?.Length == NUMBER_OF_STAR_SCORE_THRESHOLDS,
                    "Expected player star score cutoffs to contain {0} thresholds, got {1}.",
                    NUMBER_OF_STAR_SCORE_THRESHOLDS,
                    engineContainer.BaseEngine.StarScoreThresholds?.Length ?? 0);
                YargLogger.Assert(
                    engineContainer.BaseEngine.StarScoreThresholds != null,
                    "Player star score cutoffs are null.");
                debugSpPresenceCounters[(int) engineContainer.StarPowerPresenceStatus]++;
            }
            YargLogger.LogFormatDebug(
                "Star power presence counts: {0} unisons, {1} star power, {2} none.",
                debugSpPresenceCounters[0],
                debugSpPresenceCounters[1],
                debugSpPresenceCounters[2]);
#endif

            int[] bandStarScoreCutoffs = new int[NUMBER_OF_STAR_SCORE_THRESHOLDS];
            for (int i = 0; i < NUMBER_OF_STAR_SCORE_THRESHOLDS; i++)
            {
                int totalStarCutoff = 0;
                double bandSpMultiplier = 0.0;
                foreach (var engineContainer in engineContainers)
                {
                    if (engineContainer.BaseEngine.StarScoreThresholds == null)
                    {
                        YargLogger.LogWarning("Player star score cutoffs are null, skipping.");
                        continue;
                    }
                    totalStarCutoff += engineContainer.BaseEngine.StarScoreThresholds[i];
                    bandSpMultiplier += engineContainer.StarPowerPresenceStatus switch
                    {
                        // Where did .265 come from? dunno, ask Dark from 3 years ago
                        EngineContainer.ChartStarPowerPresence.HasUnisons   => 0.265,
                        EngineContainer.ChartStarPowerPresence.HasStarPower => 0.265 / 2,
                        EngineContainer.ChartStarPowerPresence.None         => 0.0,
                        _                                                   => throw new ArgumentOutOfRangeException()
                    };

                }
                double totalMultiplier = engineContainers.Count > 1
                    ? 1.0 + bandSpMultiplier * (engineContainers.Count - 1.0) / engineContainers.Count
                    : 1.0;
                bandStarScoreCutoffs[i] = (int) Math.Floor(totalStarCutoff * totalMultiplier);
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

        public void ResetStars()
        {
            _currentStarIndex = 0;
            UpdateStars();
        }
    }
}