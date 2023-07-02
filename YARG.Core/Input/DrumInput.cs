namespace YARG.Core.Input
{
    public class DrumInput : GameInput<DrumAction>
    {
        public DrumInput(DrumAction action, double time, ActionType type) : base(action, time, type)
        {
            RawValue = (int) action;
        }
    }
}