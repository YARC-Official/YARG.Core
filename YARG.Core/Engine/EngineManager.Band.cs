using System;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int Score { get; set; }
        public int Combo { get; set; }
        public float Stars { get; set; }
        public int BandMultiplier => Math.Max(_starpowerCount * 2, 1);

        public int TotalCodaBonus
        {
            get
            {
                var totalBonus = 0;
                foreach (var engine in Engines)
                {
                    totalBonus += engine.Engine.CodaBonus;
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

        private void UpdateBandMultiplier()
        {
            foreach (var engine in Engines)
            {
                engine.Engine.UpdateBandMultiplier(BandMultiplier);
            }
        }
    }
}