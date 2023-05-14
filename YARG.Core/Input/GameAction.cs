namespace YARG.Core.Input
{
    public enum GameAction : short
    {
        None = -1,
        Button1,
        Button2,
        Button3,
        Button4,
        Button5,
        Button6,
        Start,
        Select,
        Up,
        Down,
        Left,
        Right,
    }

    public enum ActionPhase : byte
    {
        Performed,
        Cancelled,
    }
}