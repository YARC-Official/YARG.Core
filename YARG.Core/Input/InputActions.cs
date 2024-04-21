namespace YARG.Core.Input
{
    // !DO NOT MODIFY THE VALUES OR ORDER OF THESE ENUMS!
    // Since they are serialized in replays, they *must* remain the same across changes.

    public enum MenuAction : byte
    {
        Green = 0,
        Red = 1,
        Yellow = 2,
        Blue = 3,
        Orange = 4,

        Up = 5,
        Down = 6,
        Left = 7,
        Right = 8,

        Start = 9,
        Select = 10,
    }

    public enum GuitarAction : byte
    {
        Fret1 = 0,
        Fret2 = 1,
        Fret3 = 2,
        Fret4 = 3,
        Fret5 = 4,
        Fret6 = 5,

        StrumUp = 6,
        StrumDown = 7,

        Whammy = 8,
        StarPower = 9,

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
        String1_Fret = 0,
        String2_Fret = 1,
        String3_Fret = 2,
        String4_Fret = 3,
        String5_Fret = 4,
        String6_Fret = 5,

        String1_Strum = 6,
        String2_Strum = 7,
        String3_Strum = 8,
        String4_Strum = 9,
        String5_Strum = 10,
        String6_Strum = 11,

        StarPower = 12,
    }

    public enum DrumsAction : byte
    {
        Drum1 = 0,
        Drum2 = 1,
        Drum3 = 2,
        Drum4 = 3,

        Cymbal1 = 4,
        Cymbal2 = 5,
        Cymbal3 = 6,

        Kick = 7,

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
        Pitch = 0,
        Hit   = 1,
    }
}