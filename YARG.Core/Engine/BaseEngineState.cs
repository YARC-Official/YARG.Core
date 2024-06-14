namespace YARG.Core.Engine
{
    public abstract class BaseEngineState
    {
        public int NoteIndex;

        public double CurrentTime;
        public double LastUpdateTime;

        public double LastQueuedInputTime;

        public uint CurrentTick;
        public uint LastTick;

        public uint DeltaTicks => CurrentTick - LastTick;

        public int CurrentSoloIndex;
        public int CurrentStarIndex;

        public bool IsSoloActive;

        public bool IsStarPowerInputActive;

        public virtual void Reset()
        {
            NoteIndex = 0;

            CurrentTime = double.MinValue;
            LastUpdateTime = double.MinValue;

            LastQueuedInputTime = double.MinValue;

            CurrentTick = 0;
            LastTick = 0;

            CurrentSoloIndex = 0;
            CurrentStarIndex = 0;

            IsSoloActive = false;

            IsStarPowerInputActive = false;
        }
    }
}