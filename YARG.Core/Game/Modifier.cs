using System;

namespace YARG.Core.Game
{
    [Flags]
    public enum Modifier
    {
        None,
        AllStrums   = 1,
        AllHopos    = 2,
        AllTaps     = 4,
        HoposToTaps = 8,
        NoteShuffle = 16,
    }
}