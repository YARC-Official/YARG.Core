using System;

namespace YARG.Core.Game
{
    [Flags]
    public enum Modifier
    {
        None        = 0,
        AllStrums   = 1 << 0,
        AllHopos    = 1 << 1,
        AllTaps     = 1 << 2,
        HoposToTaps = 1 << 3,
        NoteShuffle = 1 << 4,
    }
}