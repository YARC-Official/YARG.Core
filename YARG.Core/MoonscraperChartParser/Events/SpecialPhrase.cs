// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class SpecialPhrase : ChartObject
    {
        public enum Type
        {
            Starpower,
            Solo,

            Versus_Player1,
            Versus_Player2,

            TremoloLane,
            TrillLane,

            // RB Pro Drums
            ProDrums_Activation,

            // Vocals
            Vocals_LyricPhrase,
            Vocals_PercussionPhrase,
        }

        private readonly ID _classID = ID.Special;
        public override int classID => (int)_classID;

        public uint length;
        public Type type;

        public SpecialPhrase(uint _position, uint _length, Type _type) : base(_position)
        {
            length = _length;
            type = _type;
        }

        protected override bool Equals(SongObject b)
        {
            if (b.GetType() == typeof(SpecialPhrase))
            {
                var realB = b as SpecialPhrase;
                if (tick == realB.tick && type == realB.type)
                    return true;
                else
                    return false;
            }
            else
                return base.Equals(b);
        }

        protected override bool LessThan(SongObject b)
        {
            if (b.GetType() == typeof(SpecialPhrase))
            {
                var realB = b as SpecialPhrase;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (type < realB.type)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }

        public uint GetCappedLengthForPos(uint pos)
        {
            uint newLength;
            if (pos > tick)
                newLength = pos - tick;
            else
                newLength = 0;

            SpecialPhrase nextSp = null;
            if (song != null && chart != null)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(this, chart.specialPhrases);
                if (arrayPos == SongObjectHelper.NOTFOUND)
                    return newLength;

                while (arrayPos < chart.specialPhrases.Count - 1 && chart.specialPhrases[arrayPos].tick <= tick)
                {
                    ++arrayPos;
                }

                if (chart.specialPhrases[arrayPos].tick > tick)
                    nextSp = chart.specialPhrases[arrayPos];

                if (nextSp != null)
                {
                    // Cap sustain length
                    if (nextSp.tick < tick)
                        newLength = 0;
                    else if (pos > nextSp.tick)
                        // Cap sustain
                        newLength = nextSp.tick - tick;
                }
                // else it's the last or only special phrase
            }

            return newLength;
        }

        protected override ChartObject ChartClone() => Clone();

        public new SpecialPhrase Clone()
        {
            return new SpecialPhrase(tick, length, type)
            {
                song = song,
                chart = chart,
            };
        }

        public override string ToString()
        {
            return $"Special phrase at tick {tick} with type {type} and length {length}";
        }
    }
}
