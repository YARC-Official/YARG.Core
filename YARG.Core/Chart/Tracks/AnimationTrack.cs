using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart.Events;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{

    /// <summary>
    /// Character animation events
    /// </summary>
    public class AnimationTrack : ICloneable<AnimationTrack>
    {
        /// <value>Generic animation states (eg Idle/Play/Intense/Mellow)</value>
        public List<CharacterState> CharacterStates;
        /// <value>Left hand maps (eg chord type, no chords)</value>
        public List<HandMap>        HandMaps;
        /// <value>Right hand strum map (bass only)</value>
        public List<StrumMap>       StrumMaps;
        /// <value>Specific animation commands (eg Snare for drums or LeftHandPosition4 for guitar/bass)</value>
        public List<AnimationEvent> AnimationEvents;

        public bool IsEmpty => CharacterStates.Count == 0 && HandMaps.Count == 0 && StrumMaps.Count == 0;

        public AnimationTrack()
        {
            CharacterStates = new List<CharacterState>();
            HandMaps        = new List<HandMap>();
            StrumMaps       = new List<StrumMap>();
            AnimationEvents = new List<AnimationEvent>();
        }

        public AnimationTrack(List<CharacterState> characterStates, List<HandMap> handMaps, List<StrumMap> strumMaps, List<AnimationEvent> animationEvents)
        {
            CharacterStates = characterStates;
            HandMaps        = handMaps;
            StrumMaps       = strumMaps;
            AnimationEvents = animationEvents;
        }

        public AnimationTrack(List<CharacterState> characterStates, List<AnimationEvent> animationEvents)
        {
            CharacterStates = characterStates;
            HandMaps        = new List<HandMap>();
            StrumMaps       = new List<StrumMap>();
            AnimationEvents = animationEvents;
        }

        public AnimationTrack(AnimationTrack other) : this(other.CharacterStates.Duplicate(),
            other.HandMaps.Duplicate(), other.StrumMaps.Duplicate(), other.AnimationEvents.Duplicate())
        {
        }

        public double GetStartTime()
        {
            double minTime = 0;

            if (CharacterStates.Count > 0)
            {
                minTime = CharacterStates[0].Time;
            }

            if (HandMaps.Count > 0)
            {
                minTime = Math.Min(minTime, HandMaps[0].Time);
            }

            if (StrumMaps.Count > 0)
            {
                minTime = Math.Min(minTime, StrumMaps[0].Time);
            }

            if (AnimationEvents.Count > 0)
            {
                minTime = Math.Min(minTime, AnimationEvents[0].Time);
            }

            return minTime;
        }

        public double GetEndTime()
        {
            // The hard path
            double maxTime = 0;

            if (CharacterStates.Count > 0)
            {
                maxTime = CharacterStates[^1].TimeEnd;
            }

            if (HandMaps.Count > 0)
            {
                maxTime = Math.Max(maxTime, HandMaps[^1].TimeEnd);
            }

            if (StrumMaps.Count > 0)
            {
                maxTime = Math.Max(maxTime, StrumMaps[^1].TimeEnd);
            }

            if (AnimationEvents.Count > 0)
            {
                maxTime = Math.Max(maxTime, AnimationEvents[^1].TimeEnd);
            }

            return maxTime;
        }

        public AnimationTrack Clone()
        {
            return new AnimationTrack(this);
        }
    }
}