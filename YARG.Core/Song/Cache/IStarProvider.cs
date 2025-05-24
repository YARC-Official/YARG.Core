using System;
using YARG.Core.Game;

namespace YARG.Core.Song.Cache
{
    public interface IStarProvider
    {
        StarAmount GetBestStarsForSong(HashWrapper songHash, Guid playerId);
    }
}