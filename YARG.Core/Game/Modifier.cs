using System;

namespace YARG.Core.Game
{
    [Flags]
    public enum Modifier : ulong
    {
        None        = 0,
        AllStrums   = 1 << 0,
        AllHopos    = 1 << 1,
        AllTaps     = 1 << 2,
        HoposToTaps = 1 << 3,
        NoteShuffle = 1 << 4,
    }

    public static class ModifierExtensions
    {
        public static Modifier PossibleModifiers(this GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar =>
                    Modifier.AllStrums |
                    Modifier.AllHopos  |
                    Modifier.AllTaps   |
                    Modifier.HoposToTaps,
                GameMode.SixFretGuitar or
                GameMode.FourLaneDrums or
                GameMode.FiveLaneDrums or
            //  GameMode.TrueDrums     or
                GameMode.ProGuitar     or
                GameMode.ProKeys       or
            //  GameMode.Dj            or
                GameMode.Vocals        => Modifier.None,
                _  => throw new NotImplementedException($"Unhandled game mode {gameMode}!")
            };
        }
    }
}