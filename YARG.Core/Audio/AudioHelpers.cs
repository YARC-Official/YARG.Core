using System.Collections.Generic;

namespace YARG.Core.Audio
{
    public static class AudioHelpers
    {
        public static readonly Dictionary<string, SongStem> SupportedStems = new()
        {
            { "song",     SongStem.Song },
            { "guitar",   SongStem.Guitar },
            { "bass",     SongStem.Bass },
            { "rhythm",   SongStem.Rhythm },
            { "keys",     SongStem.Keys },
            { "vocals",   SongStem.Vocals },
            { "vocals_1", SongStem.Vocals1 },
            { "vocals_2", SongStem.Vocals2 },
            { "drums",    SongStem.Drums },
            { "drums_1",  SongStem.Drums1 },
            { "drums_2",  SongStem.Drums2 },
            { "drums_3",  SongStem.Drums3 },
            { "drums_4",  SongStem.Drums4 },
            { "crowd",    SongStem.Crowd },
            // "preview"
        };

        public static SongStem GetStemFromName(string name)
        {
            if (SupportedStems.TryGetValue(name.ToLowerInvariant(), out var stem))
                return stem;

            return default;
        }
    }
}
