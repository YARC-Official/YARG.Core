using System;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int Score { get; set; }
        public int Combo { get; set; }
        public float Stars { get; set; }
        public int BandMultiplier => Math.Max(_starpowerCount * 2, 1);
        private int BandMultiplierHuman => Math.Max(_humanStarpowerCount * 2, 1);

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier, BandMultiplierHuman);
            }
        }
    }
}