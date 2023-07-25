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
        Select,
    }

    public enum GuitarAction : byte
    {
        Fret1,
        Fret2,
        Fret3,
        Fret4,
        Fret5,
        Fret6,

        StrumUp,
        StrumDown,

        Whammy,
        StarPower,
    }

    public enum ProGuitarAction : byte
    {
        String1_Fret,
        String2_Fret,
        String3_Fret,
        String4_Fret,
        String5_Fret,
        String6_Fret,

        String1_Strum,
        String2_Strum,
        String3_Strum,
        String4_Strum,
        String5_Strum,
        String6_Strum,

        Whammy,
        StarPower,
    }

    public enum DrumsAction : byte
    {
        Tom1,
        Tom2,
        Tom3,
        Tom4,

        Cymbal1,
        Cymbal2,
        Cymbal3,

        Kick,
    }

    public enum VocalsAction : byte
    {
        Pitch,

        StarPower,
    }
}