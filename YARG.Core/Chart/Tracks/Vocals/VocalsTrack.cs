using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A vocals track.
    /// </summary>
    public class VocalsTrack
    {
        public Instrument Instrument { get; }

        public List<VocalsPart> Parts { get; } = new();

        public VocalsTrack(Instrument instrument)
        {
            Instrument = instrument;
        }

        public VocalsTrack(Instrument instrument, List<VocalsPart> parts)
            : this(instrument)
        {
            Parts = parts;
        }

        // TODO: Helper methods for getting info across all parts
    }
}