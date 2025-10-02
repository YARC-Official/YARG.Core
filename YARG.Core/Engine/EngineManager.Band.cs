using System;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int BandMultiplier => Math.Max(_starpowerCount * 2, 1);

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier);
            }
        }
    }
}
