using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public class CameraCutEvent : VenueEvent, ICloneable<CameraCutEvent>
    {

        public enum CameraCutPriority
        {
            Normal,
            Directed
        }

        public enum CameraCutConstraint
        {
            OnlyClose,
            OnlyFar,
            NoClose,
            NoBehind,
            None
        }

        public enum CameraCutSubject
        {
            Crowd,
            Stage,
            AllBehind,
            AllFar,
            AllNear,
            BehindNoDrum,
            NearNoDrum,
            Guitar,
            GuitarBehind,
            GuitarCloseup,
            GuitarCloseupHead,
            Drums,
            DrumsKick,
            DrumsBehind,
            DrumsCloseupHand,
            DrumsCloseupHead,
            Bass,
            BassBehind,
            BassCloseup,
            BassCloseupHead,
            Vocals,
            VocalsCloseup,
            VocalsBehind,
            Keys,
            KeysBehind,
            KeysCloseupHand,
            KeysCloseupHead,
            DrumsVocals,
            BassDrums,
            DrumsGuitar,
            BassVocalsBehind,
            BassVocals,
            GuitarVocalsBehind,
            GuitarVocals,
            KeysVocalsBehind,
            KeysVocals,
            BassGuitarBehind,
            BassGuitar,
            BassKeysBehind,
            BassKeys,
            GuitarKeysBehind,
            GuitarKeys,
            Random               // This needs to always be last
        }

        public CameraCutPriority      Priority      { get; }
        public CameraCutConstraint    Constraint    { get; }
        public CameraCutSubject       Subject       { get; }
        public List<CameraCutSubject> RandomChoices { get; } = new();

        public CameraCutEvent(CameraCutPriority priority, CameraCutConstraint constraint, CameraCutSubject subject, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            Priority = priority;
            Constraint = constraint;
            Subject = subject;
        }

        public CameraCutEvent(CameraCutEvent other) : base(other)
        {
            Priority = other.Priority;
            Constraint = other.Constraint;
            Subject = other.Subject;
        }

        public CameraCutEvent Clone()
        {
            return new CameraCutEvent(this);
        }
    }
}