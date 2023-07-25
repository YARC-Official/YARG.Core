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

        // 5-fret
        GreenFret  = Fret1,
        RedFret    = Fret2,
        YellowFret = Fret3,
        BlueFret   = Fret4,
        OrangeFret = Fret5,

        // 6-fret
        Black1Fret = Fret1,
        Black2Fret = Fret2,
        Black3Fret = Fret3,
        White1Fret = Fret4,
        White2Fret = Fret5,
        White3Fret = Fret6,
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
        Drum1,
        Drum2,
        Drum3,
        Drum4,

        Cymbal1,
        Cymbal2,
        Cymbal3,

        Kick,

        RedDrum    = Drum1,
        YellowDrum = Drum2,
        BlueDrum   = Drum3,
        GreenDrum  = Drum4,

        YellowCymbal = Cymbal1,
        OrangeCymbal = Cymbal2,
        BlueCymbal   = Cymbal2,
        GreenCymbal  = Cymbal3,
    }

    public enum VocalsAction : byte
    {
        Pitch,

        StarPower,
    }
}