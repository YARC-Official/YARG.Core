// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    public class SpecialPhrase : ChartObject
    {
        [Flags]
        public enum Type
        {
            Starpower,

            Versus_Player1,
            Versus_Player2,

            TremoloLane,
            TrillLane,

            // RB Pro Drums
            ProDrums_Activation,
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

        public uint GetCappedLengthForPos(uint pos)
        {
            uint newLength;
            if (pos > tick)
                newLength = pos - tick;
            else
                newLength = 0;

            SpecialPhrase nextSp = null;
            if (moonSong != null && moonChart != null)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(this, moonChart.specialPhrases);
                if (arrayPos == SongObjectHelper.NOTFOUND)
                    return newLength;

                while (arrayPos < moonChart.specialPhrases.Count - 1 && moonChart.specialPhrases[arrayPos].tick <= tick)
                {
                    ++arrayPos;
                }

                if (moonChart.specialPhrases[arrayPos].tick > tick)
                    nextSp = moonChart.specialPhrases[arrayPos];

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
    }
}
