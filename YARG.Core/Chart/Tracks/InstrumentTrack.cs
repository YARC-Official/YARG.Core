using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class InstrumentTrack<TNote>
        where TNote : Note
    {
        public Instrument Instrument { get; }
        public Difficulty MaxDifficulty { get; }

        public Dictionary<Difficulty, InstrumentDifficulty<TNote>> Difficulties { get; } = new();

        public InstrumentTrack(Instrument instrument, Difficulty maxDifficulty)
        {
            Instrument = instrument;
            MaxDifficulty = maxDifficulty;
        }

        public InstrumentTrack(Instrument instrument, Difficulty maxDifficulty,
            Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties)
            : this(instrument, maxDifficulty)
        {
            Difficulties = difficulties;
        }
    }
}