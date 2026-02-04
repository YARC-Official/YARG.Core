using System;

namespace YARG.Core.Engine
{
    /// <summary>
    /// A Coda Section (aka Big Rock Ending)
    ///
    /// During a coda section, the player can press frets or strike pads at will
    /// and can collect whatever bonus is currently available for the corresponding
    /// lane. If the player successfully plays the notes at the end of the coda
    /// section, the collected bonus score will be awarded to the player.
    /// </summary>
    public class CodaSection
    {
        // This is not the number of visible lanes on the track, this is the
        // number of notional lanes used for calculating the bonus score based
        // on fret presses or drum hits.
        // Could be 5 or 6 for Five/Six fret guitar or 1 for drums
        public int Lanes { get; private set; }

        // Last time bonus was collected for given lane
        private double[] LastCollectedTime { get; set; }

        // We need this because collected and hit are different for non-guitar instruments
        private double[] LastHitTime { get; set; }

        // Maximum bonus for one fret press or drum hit
        public int MaxLaneScore { get; private set; }

        // The total bonus that will be awarded if the BRE is successful
        public int TotalCodaBonus { get; private set; }

        public double StartTime { get; private set; }
        public double EndTime { get; private set; }

        public bool Success { get; private set; }

        private bool _fretMode = true;

        public delegate void LaneHitEvent(int lane);
        public LaneHitEvent? OnLaneHit;

        private const int MAX_DRUM_SCORE  = 750;
        private const int MAX_FRET_SCORE  = 150;

        // Time taken for bonus to recharge after collection
        private const double BONUS_RECHARGE_TIME = 1.5;

        public CodaSection(int lanes, double startTime, double endTime, bool fretMode = true)
        {
            Lanes = lanes;
            LastCollectedTime = new double[lanes];
            LastHitTime = new double[lanes];
            MaxLaneScore = fretMode ? MAX_FRET_SCORE : MAX_DRUM_SCORE;
            TotalCodaBonus = 0;
            StartTime = startTime;
            EndTime = endTime;
            _fretMode = fretMode;
            // MissNote will change this if necessary
            Success = true;
        }

        // Five fret instruments should pass something that can be interpreted as
        // a lane index. (We could take a GuitarAction or DrumsAction here, but
        // then we'd have to be a generic for no good reason)
        public void HitLane(double time, int fret)
        {
            if (fret < 0)
            {
                // How?
                return;
            }

            // Remap values that don't correspond to a lane
            if (fret > Lanes - 1)
            {
                fret %= Lanes - 1;
            }

            // Collect bonus for this lane
            if (_fretMode)
            {
                int bonusScore = GetCurrentLaneScore(fret, time);
                LastCollectedTime[fret] = time;
                TotalCodaBonus += bonusScore;
            }
            else
            {
                // Non-fret instruments only have one scoring lane
                int bonusScore = GetCurrentLaneScore(0, time);
                LastCollectedTime[0] = time;
                TotalCodaBonus += bonusScore;
            }

            LastHitTime[fret] = time;

            OnLaneHit?.Invoke(fret);
        }

        public void MissNote()
        {
            Success = false;
            TotalCodaBonus = 0;
        }

        /// <summary>
        /// Resets Coda state. Useful for replay rewind or practice mode restart.
        /// </summary>
        /// <param name="earnedBonus">Amount of Coda bonus earned up to the new song time.<br />Needed if rewinding into the middle of the Coda section.</param>
        public void Reset(int earnedBonus = 0)
        {
            Success = true;
            // TODO: Make sure we really need this
            TotalCodaBonus = earnedBonus;
        }

        public int GetCurrentLaneScore(int fret, double time)
        {
            return (int) Math.Floor((Math.Min(time - LastCollectedTime[fret], BONUS_RECHARGE_TIME) / BONUS_RECHARGE_TIME) * MaxLaneScore);
        }

        public double GetTimeSinceLastHit(int fret, double time) => time - LastCollectedTime[fret];

        /// <summary>
        /// Returns normalized time since last hit<br/>
        /// Reaches 1.0f at BONUS_RECHARGE_TIME
        /// </summary>
        /// <param name="fret"></param>
        /// <param name="time"></param>
        /// <returns>float range 0.0f to 1.0f</returns>
        public float GetNormalizedTimeSinceLastHit(int fret, double time)
        {
            return (float) (Math.Min(time - LastHitTime[fret], BONUS_RECHARGE_TIME) / BONUS_RECHARGE_TIME);
        }
    }
}