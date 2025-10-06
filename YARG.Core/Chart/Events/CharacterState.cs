using System;

namespace YARG.Core.Chart.Events
{
    public class CharacterState : ChartEvent, ICloneable<CharacterState>
    {
        public enum CharacterStateType
        {
            Idle,
            IdleIntense,
            IdleRealtime,
            Play,
            PlaySolo,
            Intense,
            Mellow
        }

        public CharacterStateType Type { get; }

        public CharacterState(CharacterStateType type, double time, uint tick) : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public CharacterState(CharacterState other) : base(other)
        {
            Type = other.Type;
        }

        public CharacterState Clone()
        {
            return new CharacterState(this);
        }
    }
}