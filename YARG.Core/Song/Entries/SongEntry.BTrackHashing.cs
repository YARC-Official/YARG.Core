using System;
using System.Collections.Concurrent;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;

namespace YARG.Core.Song
{
    public abstract partial class SongEntry
    {
        [NonSerialized]
        private ConcurrentDictionary<(Instrument Instrument, Difficulty Difficulty), BTrackHashResult> _bTrackHashCache = new();

        public bool TryGetBTrackHash(Instrument instrument, Difficulty difficulty, out BTrackHashResult result)
        {
            if (TryGetCachedBTrackHash(instrument, difficulty, out result))
            {
                return true;
            }

            var chart = LoadChart();
            return chart != null && TryGetBTrackHash(chart, instrument, difficulty, out result);
        }

        public bool TryGetBTrackHash(SongChart chart, Instrument instrument, Difficulty difficulty, out BTrackHashResult result)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            if (TryGetCachedBTrackHash(instrument, difficulty, out result))
            {
                return true;
            }

            if (!BTrackHasher.TryCalculateTrackHash(chart, instrument, difficulty, out result))
            {
                return false;
            }

            _bTrackHashCache.TryAdd((instrument, difficulty), result);
            return true;
        }

        public string GetLeaderboardHash(Instrument instrument, Difficulty difficulty)
        {
            return TryGetBTrackHash(instrument, difficulty, out var result)
                ? result.Hash
                : Hash.ToString();
        }

        private bool TryGetCachedBTrackHash(Instrument instrument, Difficulty difficulty, out BTrackHashResult result)
        {
            return _bTrackHashCache.TryGetValue((instrument, difficulty), out result);
        }
    }
}
