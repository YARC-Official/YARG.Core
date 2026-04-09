using System;
using System.Collections.Generic;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public int Score { get; set; }
        public int Combo { get; set; }
        public float Stars { get; set; }
        public int BandMultiplier => Math.Max(_starpowerCount * 2, 1);

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
    }
}