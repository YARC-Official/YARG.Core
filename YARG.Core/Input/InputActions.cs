namespace YARG.Core.Input
{

    public enum MenuAction : byte
    {
        Green,
        Red,
        Yellow,
        Blue,
        Orange,
        Up,
        Down,
        Left,
        Right,
        Start,
    }
    
    public enum GuitarAction : byte
    {
        Green,
        Red,
        Yellow,
        Blue,
        Orange,
        StrumUp,
        StrumDown,
        Pause,
        StarPower,
    }

    public enum DrumAction : byte
    {
        Tom1,
        Tom2,
        Tom3,
        Tom4,
        Cymbal1,
        Cymbal2,
        Cymbal3,
        Kick,
        Pause,
    }

    public enum ActionType : byte
    {
        Performed,
        Cancelled,
        Event
    }
}