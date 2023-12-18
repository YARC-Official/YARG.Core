// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class BPM : SongObject
    {
        public float value;
        public double? anchor = null;
        public double assignedTime = 0;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="_position">Tick position.</param>
        /// <param name="_value">The bpm value.</param>
        public BPM(uint _position = 0, float _value = 120, double? _anchor = null)
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
            return $"BPM at tick {tick} with tempo {value}";
        }
    }
}
