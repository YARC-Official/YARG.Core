using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A venue track.
    /// </summary>
    public class VenueTrack
    {
        public List<LightingEvent> Lighting { get; } = new();
        public List<PostProcessingEvent> PostProcessing { get; } = new();
        public List<PerformerEvent> Performer { get; } = new();
        public List<VenueTextEvent> Other { get; } = new();

        public VenueTrack() { }

        public VenueTrack(List<LightingEvent> lighting, List<PostProcessingEvent> postProcessing,
            List<PerformerEvent> performer, List<VenueTextEvent> other)
        {
            Lighting = lighting;
            PostProcessing = postProcessing;
            Performer = performer;
            Other = other;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            totalFirstTick = Math.Min(Lighting.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(PostProcessing.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Performer.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Other.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Lighting.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(PostProcessing.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Performer.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Other.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}