using System;

namespace YARG.Core.Song.Cache
{
    public interface IPlayerContext
    {
        Guid GetCurrentPlayerId();

        Instrument GetCurrentInstrument();

        Difficulty GetCurrentDifficulty();
    }
}