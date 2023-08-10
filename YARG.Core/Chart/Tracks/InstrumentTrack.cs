using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class InstrumentTrack<TNote> : ICloneable<InstrumentTrack<TNote>>
        where TNote : Note<TNote>
    {
        public Instrument Instrument { get; }

        public Dictionary<Difficulty, InstrumentDifficulty<TNote>> Difficulties { get; } = new();

        public InstrumentTrack(Instrument instrument)
        {
            Instrument = instrument;
        }

        public InstrumentTrack(Instrument instrument, Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties)
            : this(instrument)
        {
            Difficulties = difficulties;
        }

        public InstrumentTrack(InstrumentTrack<TNote> other)
            : this(other.Instrument)
        {
            foreach (var (difficulty, diffTrack) in other.Difficulties)
            {
                Difficulties.Add(difficulty, diffTrack.Clone());
            }
        }

        public double GetStartTime()
        {
            double totalStartTime = 0;
            foreach (var difficulty in Difficulties.Values)
            {
                totalStartTime = Math.Min(difficulty.GetStartTime(), totalStartTime);
            }

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;
            foreach (var difficulty in Difficulties.Values)
            {
                totalEndTime = Math.Max(difficulty.GetEndTime(), totalEndTime);
            }

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;
            foreach (var difficulty in Difficulties.Values)
            {
                totalFirstTick = Math.Min(difficulty.GetFirstTick(), totalFirstTick);
            }

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;
            foreach (var difficulty in Difficulties.Values)
            {
                totalLastTick = Math.Max(difficulty.GetLastTick(), totalLastTick);
            }

            return totalLastTick;
        }

        public InstrumentTrack<TNote> Clone()
        {
            return new(this);
        }
    }
}