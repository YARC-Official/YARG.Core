// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class BPM : SongObject
    {
        /// <summary>
        /// Stored as the bpm value * 1000. For example, a bpm of 120.075 would be stored as 120075.
        /// </summary>
        public uint value;
        public float displayValue => value / 1000.0f;

        public double? anchor = null;

        public double assignedTime = 0;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="_position">Tick position.</param>
        /// <param name="_value">Stored as the bpm value * 1000 to limit it to 3 decimal places. For example, a bpm of 120.075 would be stored as 120075.</param>
        public BPM(uint _position = 0, uint _value = 120000, double? _anchor = null)
            : base(ID.BPM, _position)
        {
            value = _value;
            anchor = _anchor;
        }

        protected override SongObject SongClone() => Clone();

        public new BPM Clone()
        {
            return new BPM(tick, value, anchor)
            {
                assignedTime = assignedTime,
            };
        }

        public override string ToString()
        {
            return $"BPM at tick {tick} with tempo {displayValue}";
        }
    }
}
