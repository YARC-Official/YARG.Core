namespace YARG.Core.Input
{
    public struct GameInput
    {
        public GameAction  Action { get; }
        public ActionPhase Phase  { get; }
        public double      Time   { get; set; }

        public GameInput(GameAction action, double time, ActionPhase phase)
        {
            Action = action;
            Time = time;
            Phase = phase;
        }
    }
}