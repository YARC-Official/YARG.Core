using System;
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

        // TODO: Helper methods for getting note info across all parts

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;
            foreach (var part in Parts)
            {
                totalFirstTick = Math.Min(part.GetFirstTick(), totalFirstTick);
            }

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;
            foreach (var part in Parts)
            {
                totalLastTick = Math.Max(part.GetLastTick(), totalLastTick);
            }

            return totalLastTick;
        }
    }
}