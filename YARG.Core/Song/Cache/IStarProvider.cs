using System;
using YARG.Core.Game;

namespace YARG.Core.Song.Cache
{
    public interface IStarProvider
    {
        public StarAmount GetBestStarsForSong(HashWrapper songHash, Guid playerId, Instrument instrument, Difficulty difficulty);
    }
}