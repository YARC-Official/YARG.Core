namespace YARG.Core.Input
{
    public class DrumInput : AbstractGameInput<DrumAction>
    {
        public DrumInput(DrumAction action, double time, ActionType type) : base(action, time, type)
        {
            RawValue = (int) action;
        }
    }
}