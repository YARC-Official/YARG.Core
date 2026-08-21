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

            // We need to do the same for camera cut events
            MoonVenue? cameraCutCurrentEvent = null;
            var currentCutConstraints = CameraCutEvent.CameraCutConstraint.None;
            List<CameraCutEvent.CameraCutSubject> currentCutSubjects = new();

            // And for post processing, it turns out
            MoonVenue? postProcessingCurrentEvent = null;

            // A sixteenth note, in ticks
            var sixteenthNote = _moonSong.syncTrack.Resolution / 4;

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

                        HandlePostProcessingEvent(postProcessingEvents, type, moonVenue, ref postProcessingCurrentEvent);
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
                        HandleCameraCutEvent(cameraCutEvents, moonVenue, ref cameraCutCurrentEvent, ref currentCutConstraints, ref currentCutSubjects);
                        break;
                    }

                    case VenueLookup.Type.CameraCutConstraint:
                    {
                        HandleCameraCutEvent(cameraCutEvents, moonVenue, ref cameraCutCurrentEvent, ref currentCutConstraints, ref currentCutSubjects);
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
            FinalizePerformerEvent(performerEvents, PerformerEventType.Spotlight, ref spotlightCurrentEvent, spotlightPerformers);
            FinalizePerformerEvent(performerEvents, PerformerEventType.Singalong, ref singalongCurrentEvent, singalongPerformers);
            if (postProcessingCurrentEvent != null &&
                PostProcessLookup.TryGetValue(postProcessingCurrentEvent.text, out var postProcType))
            {
                FinalizePostProcessingEvent(postProcessingEvents, postProcType, ref postProcessingCurrentEvent);
            }

            lightingEvents.TrimExcess();
            postProcessingEvents.TrimExcess();
            performerEvents.TrimExcess();
            stageEvents.TrimExcess();
            cameraCutEvents.TrimExcess();

            return new(lightingEvents, postProcessingEvents, performerEvents, stageEvents, cameraCutEvents);
        }

        private void HandleCameraCutEvent(List<CameraCutEvent> events, MoonVenue moonEvent,
            ref MoonVenue? currentEvent, ref CameraCutEvent.CameraCutConstraint constraints, ref List<CameraCutEvent.CameraCutSubject> currentCutSubjects)
        {
            // First event
            if (currentEvent == null)
            {
                // It's possible we got the constraint before the subject, so check for that
                if (moonEvent.type == VenueLookup.Type.CameraCutConstraint)
                {
                    if (!CameraCutConstraintLookup.TryGetValue(moonEvent.text, out constraints))
                    {
                        // Invalid event, so just return (this should never be possible)
                        YargLogger.LogFormatDebug("Invalid camera cut constraint '{0}'!", moonEvent.text);
                        return;
                    }
                }
                else
                {
                    if (!CameraCutSubjectLookup.TryGetValue(moonEvent.text, out var currentCutSubject))
                    {
                        // Invalid event, so just return (this should never be possible)
                        YargLogger.LogFormatDebug("Invalid camera cut subject '{0}'!", moonEvent.text);
                        return;
                    }

                    // If we are here, we are at the first event and were not preceded by a constraint
                    currentCutSubjects.Add(currentCutSubject);
                    constraints = CameraCutEvent.CameraCutConstraint.None;
                }

                currentEvent = moonEvent;
            }
            else if (currentEvent.tick != moonEvent.tick)
            {
                // Moving on to the next event, so save previous
                double time = _moonSong.TickToTime(currentEvent.tick);
                double length = GetLengthInTime(currentEvent);
                var subject = CameraCutEvent.CameraCutSubject.Random;

                if (currentCutSubjects.Count == 1)
                {
                    subject = currentCutSubjects[0];
                }

                var cameraCut = new CameraCutEvent(CameraCutEvent.CameraCutPriority.Normal, constraints, subject, time, length, currentEvent.tick, currentEvent.length);

                if (currentCutSubjects.Count > 1)
                {
                    // It's only a choice if there is more than one
                    cameraCut.RandomChoices.AddRange(currentCutSubjects);

                    // Also, remove random because it makes no sense in this context
                    cameraCut.RandomChoices.RemoveAll(x => x == CameraCutEvent.CameraCutSubject.Random);
                }

                events.Add(cameraCut);

                currentEvent = moonEvent;
                constraints = CameraCutEvent.CameraCutConstraint.None;
                currentCutSubjects.Clear();
            }

            // we could have gotten the subject or a constraint first, so act accordingly
            if (moonEvent.type == VenueLookup.Type.CameraCutConstraint)
            {
                if (CameraCutConstraintLookup.TryGetValue(moonEvent.text, out var constraint))
                {
                    constraints |= constraint;
                }
            }
            else if (moonEvent.type == VenueLookup.Type.CameraCut)
            {
                if (CameraCutSubjectLookup.TryGetValue(moonEvent.text, out var subject))
                {
                    currentCutSubjects.Add(subject);
                }
            }
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
                FinalizePerformerEvent(events, type, ref currentEvent, performers);

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
            ref MoonVenue? currentEvent,
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

        private void HandlePostProcessingEvent(
            List<PostProcessingEvent> events,
            PostProcessingType type,
            MoonVenue moonEvent,
            ref MoonVenue? currentEvent
        )
        {
            // No currently tracked event
            if (currentEvent == null)
            {
                // If there is a length, this must have come from a note, so we can add it
                // directly without tracking
                if (moonEvent.length > 0)
                {
                    var sixteenthNote = _moonSong.syncTrack.Resolution / 4;

                    // If it is less than a 16th note, treat it as if the length were 0
                    if (moonEvent.length < sixteenthNote)
                    {
                        moonEvent.length = 0;
                    }

                    currentEvent = moonEvent;
                    FinalizePostProcessingEvent(events, type, ref currentEvent);
                    return;
                }

                // Having no length, this is a text event, so we must track it until we get called again
                currentEvent = moonEvent;
                return;
            }

            // If this is the same type as the tracked event, give the tracked event a length and ignore this one
            if (moonEvent.type == currentEvent.type)
            {
                uint length = moonEvent.tick - currentEvent.tick;
                currentEvent.length = length;
                FinalizePostProcessingEvent(events, type, ref currentEvent);
                return;
            }

            // Different than previous, so there was no length
            FinalizePostProcessingEvent(events, type, ref currentEvent);
            currentEvent = moonEvent;
        }

        private void FinalizePostProcessingEvent(
            List<PostProcessingEvent> events,
            PostProcessingType type,
            ref MoonVenue? currentEvent
        )
        {
            if (currentEvent != null)
            {
                events.OrderedInsert(new(type,
                    _moonSong.TickToTime(currentEvent.tick),
                    GetLengthInTime(currentEvent),
                    currentEvent.tick,
                    currentEvent.length));
            }

            currentEvent = null;
        }

        private double GetLengthInTime(MoonVenue ev)
        {
            double time = _moonSong.TickToTime(ev.tick);
            return GetLengthInTime(time, ev.tick, ev.length);
        }
    }
}