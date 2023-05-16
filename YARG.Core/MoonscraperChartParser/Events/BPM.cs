// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class BPM : SyncTrack
    {
        private readonly ID _classID = ID.BPM;

        public override int classID { get { return (int)_classID; } }

        /// <summary>
        /// Stored as the bpm value * 1000. For example, a bpm of 120.075 would be stored as 120075.
        /// </summary>
        public uint value;
        public float displayValue
        {
            get
            {
                return (float)value / 1000.0f;
            }
        }

        public double? anchor = null;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="_position">Tick position.</param>
        /// <param name="_value">Stored as the bpm value * 1000 to limit it to 3 decimal places. For example, a bpm of 120.075 would be stored as 120075.</param>
        public BPM(uint _position = 0, uint _value = 120000, float? _anchor = null) : base(_position)
        {
            value = _value;
            anchor = _anchor;
        }
    }
}
