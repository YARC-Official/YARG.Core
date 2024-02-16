namespace YARG.Core.Engine
{
    public abstract class BaseEngineState
    {
        public int NoteIndex;

        public TimeContext TimeContext;

        public double LastQueuedInputTime;

        public int CurrentTimeSigIndex;
        public int NextTimeSigIndex;

        public uint TicksEveryBeat;
        public uint TicksEveryMeasure;

        public int CurrentSoloIndex;
        public int CurrentStarIndex;

        public bool IsSoloActive;

        public bool IsStarPowerInputActive;
        public uint StarPowerBaseTick;

        public double CurrentTime => TimeContext.Time;
        public uint CurrentTick => TimeContext.Tick;

        public virtual void Reset()
        {
            NoteIndex = 0;

            TimeContext = TimeContext.Create();

            LastQueuedInputTime = double.MinValue;

            CurrentTimeSigIndex = 0;
            NextTimeSigIndex = 1;

            TicksEveryBeat = 0;
            TicksEveryMeasure = 0;

            CurrentSoloIndex = 0;
            CurrentStarIndex = 0;

            IsSoloActive = false;

            IsStarPowerInputActive = false;
            StarPowerBaseTick = 0;
        }
    }
}