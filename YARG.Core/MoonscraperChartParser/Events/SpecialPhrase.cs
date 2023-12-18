// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class SpecialPhrase : SongObject
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

        public uint length;
        public Type type;

        public SpecialPhrase(uint _position, uint _length, Type _type)
            : base(ID.Special, _position)
        {
            length = _length;
            type = _type;
        }

        public override bool ValueEquals(SongObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not SpecialPhrase phrase)
                return baseEq;

            return type == phrase.type;
        }

        public override int InsertionCompareTo(SongObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not SpecialPhrase phrase)
                return baseComp;

            return ((int) type).CompareTo((int) phrase.type);
        }

        public uint GetCappedLengthForPos(uint pos, MoonChart? chart)
        {
            uint newLength;
            if (pos > tick)
                newLength = pos - tick;
            else
                newLength = 0;

            SpecialPhrase? nextSp = null;
            if (chart != null)
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

        protected override SongObject SongClone() => Clone();

        public new SpecialPhrase Clone()
        {
            return new SpecialPhrase(tick, length, type);
        }

        public override string ToString()
        {
            return $"Special phrase at tick {tick} with type {type}, length {length}";
        }
    }
}
