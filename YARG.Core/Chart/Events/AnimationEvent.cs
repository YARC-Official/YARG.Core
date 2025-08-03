using System;
using System.Diagnostics.CodeAnalysis;

namespace YARG.Core.Chart
{
    public class AnimationEvent : ChartEvent, ICloneable<AnimationEvent>
    {
        public AnimationType Type { get; }

        public AnimationEvent(AnimationType type, double time, double timeLength, uint tick, uint tickLength) : base(time, timeLength, tick, tickLength)
        {
            Type = type;
        }

        public AnimationEvent(AnimationEvent other) : base(other)
        {
            Type = other.Type;
        }

        public AnimationEvent Clone()
        {
            return new AnimationEvent(this);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum AnimationType
        {
            Kick,
            OpenHiHat,
            SnareLhHard,
            SnareRhHard,
            SnareLhSoft,
            SnareRhSoft,
            HihatLeftHand,
            HihatRightHand,
            PercussionRightHand,
            Crash1LhHard,
            Crash1LhSoft,
            Crash1RhHard,
            Crash1RhSoft,
            Crash2RhHard,
            Crash2RhSoft,
            Crash1Choke,
            Crash2Choke,
            RideRh,
            RideLh,
            Crash2LhHard,
            Crash2LhSoft,
            Tom1LeftHand,
            Tom1RightHand,
            Tom2LeftHand,
            Tom2RightHand,
            FloorTomLeftHand,
            FloorTomRightHand,
            LeftHandPosition1,
            LeftHandPosition2,
            LeftHandPosition3,
            LeftHandPosition4,
            LeftHandPosition5,
            LeftHandPosition6,
            LeftHandPosition7,
            LeftHandPosition8,
            LeftHandPosition9,
            LeftHandPosition10,
            LeftHandPosition11,
            LeftHandPosition12,
            LeftHandPosition13,
            LeftHandPosition14,
            LeftHandPosition15,
            LeftHandPosition16,
            LeftHandPosition17,
            LeftHandPosition18,
            LeftHandPosition19,
            LeftHandPosition20,
            CloseHiHat, // Not actually a note type, this just happens when the OpenHiHat note ends
        }
    }
}