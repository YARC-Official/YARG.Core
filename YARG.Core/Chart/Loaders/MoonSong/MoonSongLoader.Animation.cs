using System.Collections.Generic;
using YARG.Core.Chart.Events;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader
    {
        public AnimationTrack LoadAnimationTrack()
        {
            var characterStates = new List<CharacterState>();
            var handMaps        = new List<HandMap>();
            var strumMaps       = new List<StrumMap>();
            var animationEvents = new List<AnimationEvent>();



            return new AnimationTrack(characterStates, handMaps, strumMaps, animationEvents);
        }
    }
}