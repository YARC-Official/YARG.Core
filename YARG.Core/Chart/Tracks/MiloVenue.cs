using System;
using System.Collections.Generic;
using YARG.Core.Chart.Events;
using YARG.Core.IO;
using YARG.Core.Song;
using MiloAnimationEvent = YARG.Core.IO.MiloAnimation.MiloAnimationEvent;
using MiloAnimationType = YARG.Core.IO.MiloAnimation.MiloAnimationType;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Turns MiloAnimation data into a form more usable elsewhere in YARG
    /// </summary>
    public class MiloVenue
    {
        public List<CharacterState>      CharacterStates      { get; } = new();
        public List<CameraCutEvent>      CameraCuts           { get; } = new();
        public List<CrowdEvent>          CrowdEvents          { get; } = new();
        public List<PostProcessingEvent> PostProcessingEvents { get; } = new();
        public List<LightingEvent>       LightingEvents       { get; } = new();
        public List<StageEffectEvent>    StageEvents          { get; } = new();
        public List<PerformerEvent>      PerformerEvents      { get; } = new();

        private List<MiloAnimationEvent> _rawEvents = new();

        private readonly SongChart _chart;
        private readonly SongEntry _song;

        private          int       _rawEventIndex;

        public MiloVenue(SongChart chart, SongEntry song)
        {
            _chart     = chart;
            _song      = song;
        }

        public void Load()
        {
            var miloData = _song.LoadMiloData();
            if (miloData is { Length: > 0 })
            {
                var miloReader = new MiloAnimation(miloData);
                _rawEvents = miloReader.GetMiloAnimation();

                // Dispose of the FixedArray and the reader (which has its own FixedArray)
                miloData.Dispose();
                miloReader.Dispose();
            }
            else
            {
                // We couldn't load the milo data, so we'll just return
                return;
            }

            for (; _rawEventIndex < _rawEvents.Count; _rawEventIndex++)
            {
                // Dispatch to processing function based on type, we will regain control when all of that type are done
                var rawEvent = _rawEvents[_rawEventIndex];
                switch (rawEvent.Type)
                {
                    case MiloAnimationType.Lights:
                    case MiloAnimationType.Keyframe:
                        HandleLighting();
                        break;
                    case MiloAnimationType.ShotBassGuitar:
                        HandleCameraCuts();
                        break;
                    case MiloAnimationType.PostProcessing:
                        HandlePostProcessing();
                        break;
                    case MiloAnimationType.Fog:
                        HandleFog();
                        break;
                    case MiloAnimationType.Part2Sing:
                    case MiloAnimationType.Part3Sing:
                    case MiloAnimationType.Part4Sing:
                    case MiloAnimationType.SpotBass:
                    case MiloAnimationType.SpotGuitar:
                    case MiloAnimationType.SpotDrums:
                    case MiloAnimationType.SpotVocal:
                    case MiloAnimationType.SpotKeyboard:
                        HandlePerformer();
                        break;
                    case MiloAnimationType.WorldEvent:
                        HandleStage();
                        break;
                    default:
                        _rawEventIndex++;
                        continue;
                }
            }

            // Sort all the lists
            CameraCuts.Sort((a, b) => a.Time.CompareTo(b.Time));
            CrowdEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            PostProcessingEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            LightingEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            StageEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
            PerformerEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private void HandleStage()
        {
            while (_rawEventIndex < _rawEvents.Count && _rawEvents[_rawEventIndex].Type == MiloAnimationType.WorldEvent)
            {
                if (_rawEvents[_rawEventIndex].Name == "bonusfx")
                {
                    StageEvents.Add(new StageEffectEvent(StageEffect.BonusFx, VenueEventFlags.None,
                        _rawEvents[_rawEventIndex].Time, _chart.SyncTrack.TimeToTick(_rawEvents[_rawEventIndex].Time)));
                }

                _rawEventIndex++;
            }
        }

        private void HandleFog()
        {
            while (_rawEventIndex < _rawEvents.Count && _rawEvents[_rawEventIndex].Type == MiloAnimationType.Fog)
            {
                if (_rawEvents[_rawEventIndex].Name == "on")
                {
                    StageEvents.Add(new StageEffectEvent(StageEffect.FogOn, VenueEventFlags.None,
                        _rawEvents[_rawEventIndex].Time, _chart.SyncTrack.TimeToTick(_rawEvents[_rawEventIndex].Time)));
                }
                else if (_rawEvents[_rawEventIndex].Name == "off")
                {
                    StageEvents.Add(new StageEffectEvent(StageEffect.FogOff, VenueEventFlags.None,
                        _rawEvents[_rawEventIndex].Time, _chart.SyncTrack.TimeToTick(_rawEvents[_rawEventIndex].Time)));
                }

                _rawEventIndex++;
            }
        }

        private void HandlePostProcessing()
        {
            while (_rawEventIndex < _rawEvents.Count &&
                _rawEvents[_rawEventIndex].Type == MiloAnimationType.PostProcessing)
            {
                var rawEvent = _rawEvents[_rawEventIndex];
                // Try to get the corresponding PP type from VenueLookup.PostProcessingLookup
                if (VenueLookup.VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(rawEvent.Name, out var ppLookup))
                {
                    if (!VenueLookup.PostProcessLookup.TryGetValue(ppLookup.text, out var ppType))
                    {
                        continue;
                    }

                    PostProcessingEvents.Add(new PostProcessingEvent(ppType, rawEvent.Time,
                        _chart.SyncTrack.TimeToTick(rawEvent.Time)));
                }

                _rawEventIndex++;
            }
        }

        private void HandleCameraCuts()
        {
            while (_rawEventIndex < _rawEvents.Count &&
                _rawEvents[_rawEventIndex].Type == MiloAnimationType.ShotBassGuitar)
            {
                var rawEvent = _rawEvents[_rawEventIndex];
                var name = rawEvent.Name;
                if (rawEvent.Name.StartsWith("coop_"))
                {
                    name = rawEvent.Name[5..];
                }
                if (VenueLookup.CameraCutSubjectLookup.TryGetValue(name, out var cameraCutSubject))
                {
                    uint tick = _chart.SyncTrack.TimeToTick(rawEvent.Time);
                    double length = 0;
                    uint tickLength = 0;
                    var priority = rawEvent.Name.StartsWith("directed") ? CameraCutEvent.CameraCutPriority.Directed : CameraCutEvent.CameraCutPriority.Normal;

                    // If there is another event after this one, it dictates our length
                    if (_rawEventIndex + 1 < _rawEvents.Count &&
                        _rawEvents[_rawEventIndex + 1].Type == MiloAnimationType.ShotBassGuitar)
                    {
                        var nextTime = _rawEvents[_rawEventIndex + 1].Time - double.Epsilon;
                        length = nextTime - rawEvent.Time;
                        tickLength = _chart.SyncTrack.TimeToTick(nextTime) - tick - 1;
                    }

                    CameraCuts.Add(new CameraCutEvent(priority, CameraCutEvent.CameraCutConstraint.None,
                        cameraCutSubject, rawEvent.Time, length, tick, tickLength));
                }

                _rawEventIndex++;
            }
        }

        private void HandleLighting()
        {
            while (_rawEventIndex < _rawEvents.Count &&
                _rawEvents[_rawEventIndex].Type is MiloAnimationType.Keyframe or MiloAnimationType.Lights)
            {
                var rawEvent = _rawEvents[_rawEventIndex];
                // Internal names for some lighting cues are different from what is in the file
                var name = rawEvent.Name;
                if (VenueLookup.VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(name, out var lightingLookup))
                {
                    name = lightingLookup;
                }

                if (VenueLookup.LightingLookup.TryGetValue(name, out var lighting))
                {
                    LightingEvents.Add(new LightingEvent(lighting, rawEvent.Time, _chart.SyncTrack.TimeToTick(rawEvent.Time)));
                }

                _rawEventIndex++;
            }
        }

        private void HandlePerformer()
        {
            // There has got to be a better way to do this...it works because these parts all happen to be consecutive
            while (_rawEventIndex < _rawEvents.Count && _rawEvents[_rawEventIndex].Type is MiloAnimationType.Part2Sing
                or MiloAnimationType.Part3Sing or MiloAnimationType.Part4Sing or MiloAnimationType.SpotGuitar
                or MiloAnimationType.SpotBass or MiloAnimationType.SpotDrums or MiloAnimationType.SpotVocal
                or MiloAnimationType.SpotKeyboard)
            {
                // Given the file format, we should never be the last event, but we'll check. If we do happen to be
                // the last event, we'll skip since it logically has to be an off which we already saw and even if
                // it isn't, we don't want to set a hanging on.
                if (_rawEventIndex + 1 >= _rawEvents.Count)
                {
                    _rawEventIndex++;
                    break;
                }

                var rawEvent = _rawEvents[_rawEventIndex];
                var nextEvent = _rawEvents[_rawEventIndex + 1];

                // If the event is on, check that the next is off and if so calculate the length and add to the list
                if ((rawEvent.Name == "on" && nextEvent.Type == rawEvent.Type && nextEvent.Name == "off") ||
                    (rawEvent.Name == "singalong_on" && nextEvent.Type == rawEvent.Type && nextEvent.Name == "singalong_off"))
                {
                    var time = rawEvent.Time;
                    var tick = _chart.SyncTrack.TimeToTick(time);
                    var length = nextEvent.Time - time;
                    var tickLength = _chart.SyncTrack.TimeToTick(nextEvent.Time) - tick - 1;

                    PerformerEvents.Add(new PerformerEvent(PerformerEventLookup[rawEvent.Type],
                        PerformerLookup[rawEvent.Type], time, length, tick, tickLength));
                }

                _rawEventIndex++;
            }

            // Now we need to sort the list and coalesce any that fall on the same tick
            PerformerEvents.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            // Reverse search since we will likely be removing elements
            for (var i = PerformerEvents.Count - 1; i > 0; i--)
            {
                var a = PerformerEvents[i];
                var b = PerformerEvents[i - 1];

                if (a.Tick == b.Tick && a.Type == b.Type)
                {
                    // Create a combined event, replace i - 1, and remove i
                    var performers = a.Performers | b.Performers;
                    PerformerEvents[i - 1] = new PerformerEvent(b.Type, performers, b.Time, a.TimeLength, b.Tick, a.TickLength);
                    PerformerEvents.RemoveAt(i);
                }
            }
        }

        private static readonly Dictionary<MiloAnimationType, Performer> PerformerLookup = new()
        {
            { MiloAnimationType.Part2Sing, Performer.Guitar },
            { MiloAnimationType.Part3Sing, Performer.Bass },
            { MiloAnimationType.Part4Sing, Performer.Drums },
            { MiloAnimationType.SpotGuitar, Performer.Guitar },
            { MiloAnimationType.SpotBass, Performer.Bass },
            { MiloAnimationType.SpotDrums, Performer.Drums },
            { MiloAnimationType.SpotVocal, Performer.Vocals },
            { MiloAnimationType.SpotKeyboard, Performer.Keyboard }
        };

        private static readonly Dictionary<MiloAnimationType, PerformerEventType> PerformerEventLookup = new()
        {
            { MiloAnimationType.Part2Sing, PerformerEventType.Singalong },
            { MiloAnimationType.Part3Sing, PerformerEventType.Singalong },
            { MiloAnimationType.Part4Sing, PerformerEventType.Singalong },
            { MiloAnimationType.SpotGuitar, PerformerEventType.Spotlight },
            { MiloAnimationType.SpotBass, PerformerEventType.Spotlight },
            { MiloAnimationType.SpotDrums, PerformerEventType.Spotlight },
            { MiloAnimationType.SpotVocal, PerformerEventType.Spotlight },
            { MiloAnimationType.SpotKeyboard, PerformerEventType.Spotlight }
        };
    }
}