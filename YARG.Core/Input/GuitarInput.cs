namespace YARG.Core.Input
{
    public class GuitarInput : GameInput<GuitarAction>
    {
        public GuitarInput(GuitarAction action, double time, ActionType type) : base(action, time, type)
        {
            RawValue = (int) action;
        }
    }
}