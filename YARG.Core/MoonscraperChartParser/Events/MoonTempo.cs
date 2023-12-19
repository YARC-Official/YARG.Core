// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonTempo : MoonObject
    {
        public float value;
        public double? anchor = null;
        public double assignedTime = 0;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="_position">Tick position.</param>
        /// <param name="_value">The bpm value.</param>
        public MoonTempo(uint _position = 0, float _value = 120, double? _anchor = null)
            : base(ID.BPM, _position)
        {
            value = _value;
            anchor = _anchor;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonTempo bpm)
                return baseEq;

            return value == bpm.value &&
                anchor == bpm.anchor &&
                assignedTime == bpm.assignedTime;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonTempo Clone()
        {
            return new MoonTempo(tick, value, anchor)
            {
                assignedTime = assignedTime,
            };
        }

        public override string ToString()
        {
            return $"BPM at tick {tick} with tempo {value}, anchor {anchor?.ToString() ?? "(none)"}, time {assignedTime}";
        }
    }
}
