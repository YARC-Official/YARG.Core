using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Chart
{
    using static VenueLookup;

    internal partial class MoonSongLoader : ISongLoader
    {

        public VenueTrack LoadVenueTrack()
        {
            var lightingEvents = new List<LightingEvent>();
            var postProcessingEvents = new List<PostProcessingEvent>();
            var performerEvents = new List<PerformerEvent>();
            var stageEvents = new List<StageEffectEvent>();
            var cameraCutEvents = new List<CameraCutEvent>();

            // For merging spotlights/singalongs into a single event
            MoonVenue? spotlightCurrentEvent = null;
            MoonVenue? singalongCurrentEvent = null;
            var spotlightPerformers = Performer.None;
            var singalongPerformers = Performer.None;

            foreach (var moonVenue in _moonSong.venue)
            {
                // Prefix flags
                var splitter = moonVenue.text.AsSpan().Split(' ');
                splitter.MoveNext();
                var flags = VenueEventFlags.None;
                foreach (var (prefix, flag) in FlagPrefixLookup)
                {
                    if (splitter.Current.Equals(prefix, StringComparison.Ordinal))
                    {
                        flags |= flag;
                        splitter.MoveNext();
                    }
                }

                // Taking the allocation L here, the only way to access with a span is by going over
                // all the key-value pairs, which is 5x slower at even just 25 elements (O(n) vs O(1) with a string)
                // There's a lot of other allocations happening here anyways lol
                string text = splitter.CurrentToEnd.ToString();
                switch (moonVenue.type)
                {
                    case VenueLookup.Type.Lighting:
                    {
                        if (!LightingLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        lightingEvents.Add(new(type, time, moonVenue.tick));
                        break;
                    }

                    case VenueLookup.Type.PostProcessing:
                    {
                        if (!PostProcessLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        postProcessingEvents.Add(new(type, time, moonVenue.tick));
                        break;
                    }

                    case VenueLookup.Type.Singalong:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Singalong, moonVenue,
                            ref singalongCurrentEvent, ref singalongPerformers);
                        break;
                    }

                    case VenueLookup.Type.Spotlight:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Spotlight, moonVenue,
                            ref spotlightCurrentEvent, ref spotlightPerformers);
                        break;
                    }

                    case VenueLookup.Type.StageEffect:
                    {
                        if (!StageEffectLookup.TryGetValue(text, out var type))
                            continue;

                        double time = _moonSong.TickToTime(moonVenue.tick);
                        stageEvents.Add(new(type, flags, time, moonVenue.tick));
                        break;
                    }

                    case VenueLookup.Type.CameraCut:
                    {
                        double time = _moonSong.TickToTime(moonVenue.tick);
                        if (!CameraCutSubjectLookup.TryGetValue(text, out var subject))
                        {
                            continue;
                        }

                        // Hopes and dreams say that the lower note number will be processed first
                        // TODO: Don't rely on hopes and dreams
                        if (cameraCutEvents.Count > 0 && cameraCutEvents[^1].Tick == moonVenue.tick)
                        {
                            cameraCutEvents[^1].RandomChoices.Add(subject);
                            continue;
                        }

                        var length = GetLengthInTime(moonVenue);

                        // TODO: Actually use the correct priority and constraints, but it doesn't matter for now
                        // since they aren't implemented in the venue camera system yet...
                        cameraCutEvents.Add(new(CameraCutEvent.CameraCutPriority.Directed,
                            CameraCutEvent.CameraCutConstraint.None, subject, time, length, moonVenue.tick, moonVenue.length));
                        break;
                }

                    default:
                    {
                        YargLogger.LogFormatDebug("Unrecognized venue text event '{0}'!", text);
                        continue;
                    }
                }
            }

            // Flush tracked events
            FinalizePerformerEvent(performerEvents, PerformerEventType.Spotlight, spotlightCurrentEvent, spotlightPerformers);
            FinalizePerformerEvent(performerEvents, PerformerEventType.Singalong, singalongCurrentEvent, singalongPerformers);

            lightingEvents.TrimExcess();
            postProcessingEvents.TrimExcess();
            performerEvents.TrimExcess();
            stageEvents.TrimExcess();
            cameraCutEvents.TrimExcess();

            return new(lightingEvents, postProcessingEvents, performerEvents, stageEvents, cameraCutEvents);
        }

        private void HandlePerformerEvent(
            List<PerformerEvent> events,
            PerformerEventType type,
            MoonVenue moonEvent,
            ref MoonVenue? currentEvent,
            ref Performer performers
        )
        {
            // First event
            if (currentEvent == null)
            {
                currentEvent = moonEvent;
            }
            // Start of a new event
            else if (currentEvent.tick != moonEvent.tick && performers != Performer.None)
            {
                FinalizePerformerEvent(events, type, currentEvent, performers);

                // Track new event
                currentEvent = moonEvent;
                performers = Performer.None;
            }

            // Sing-along events are not optional, use the text directly
            if (PerformerLookup.TryGetValue(moonEvent.text, out var performer))
            {
                performers |= performer;
            }
        }

        private void FinalizePerformerEvent(
            List<PerformerEvent> events,
            PerformerEventType type,
            MoonVenue? currentEvent,
            Performer performers
        )
        {
            if (currentEvent != null)
            {
                events.OrderedInsert(new(
                    type,
                    performers,
                    _moonSong.TickToTime(currentEvent.tick),
                    GetLengthInTime(currentEvent),
                    currentEvent.tick,
                    currentEvent.length
                ));
            }
        }

        private double GetLengthInTime(MoonVenue ev)
        {
            double time = _moonSong.TickToTime(ev.tick);
            return GetLengthInTime(time, ev.tick, ev.length);
        }
    }
}