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
    }
}