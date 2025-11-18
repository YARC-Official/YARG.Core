using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A vocals track.
    /// </summary>
    public class VocalsTrack : ICloneable<VocalsTrack>
    {
        public Instrument Instrument { get; }

        public List<VocalsPart> Parts { get; } = new();
        public List<VocalsRangeShift> RangeShifts { get; } = new();
        public AnimationTrack Animations { get; } = new();

        /// <summary>
        /// Whether or not this track contains any data.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var part in Parts)
                {
                    if (!part.IsEmpty)
                        return false;
                }

                return true;
            }
        }

        public VocalsTrack(Instrument instrument)
        {
            Instrument = instrument;
        }

        public VocalsTrack(Instrument instrument, List<VocalsPart> parts, List<VocalsRangeShift> rangeShifts)
            : this(instrument)
        {
            Parts = parts;
            RangeShifts = rangeShifts;
        }

        public VocalsTrack(Instrument instrument, List<VocalsPart> parts, List<VocalsRangeShift> rangeShifts,
            AnimationTrack animations) : this(instrument, parts, rangeShifts)
        {
            Animations = animations;
        }

        public VocalsTrack(VocalsTrack other)
            : this(other.Instrument, other.Parts.Duplicate(), other.RangeShifts.Duplicate(), other.Animations.Clone())
        {
        }

        // TODO: Helper methods for getting note info across all parts

        /// <summary>
        /// Gets the start time of the first event in any vocals part
        /// </summary>
        /// <returns>double</returns>
        /// <remarks>This returns double.MaxValue if there are no events</remarks>
        public double GetStartTime()
        {
            double totalStartTime = double.MaxValue;
            foreach (var part in Parts)
            {
                totalStartTime = Math.Min(part.GetStartTime(), totalStartTime);
            }

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;
            foreach (var part in Parts)
            {
                totalEndTime = Math.Max(part.GetEndTime(), totalEndTime);
            }

            return totalEndTime;
        }

        public double GetFirstNoteStartTime()
        {
            double startTime = double.MaxValue;
            foreach (var part in Parts)
            {
                startTime = Math.Min(part.GetFirstNoteStartTime(), startTime);
            }

            return startTime;
        }

        public double GetLastNoteEndTime()
        {
            double endTime = 0;
            foreach (var part in Parts)
            {
                endTime = Math.Max(part.GetLastNoteEndTime(), endTime);
            }

            return endTime;
        }

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

        public VocalsTrack Clone()
        {
            return new(this);
        }
    }
}