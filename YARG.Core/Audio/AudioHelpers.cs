using System.Collections.Generic;

namespace YARG.Core.Audio
{
    public struct SfxInfo
    {
        public SfxSample Type;
        public double Volume;

        public SfxInfo(SfxSample type, double volume)
        {
            Type = type;
            Volume = volume;
        }
    }

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

        public static readonly Dictionary<string, SfxInfo> SoundEffects = new()
        {
            { "note_miss",         new(SfxSample.NoteMiss, 0.5) },
            { "starpower_award",   new(SfxSample.StarPowerAward, 0.45) },
            { "starpower_gain",    new(SfxSample.StarPowerGain, 0.5) },
            { "starpower_deploy",  new(SfxSample.StarPowerDeploy, 0.45) },
            { "starpower_release", new(SfxSample.StarPowerRelease, 0.5) },
            { "clap",              new(SfxSample.Clap, 0.15) },
            { "star",              new(SfxSample.StarGain, 1.0) },
            { "star_gold",         new(SfxSample.StarGold, 1.0) },
        };

        public static readonly List<SongStem> PitchBendAllowedStems = new()
        {
            SongStem.Guitar,
            SongStem.Bass,
            SongStem.Rhythm,
        };

        public static SongStem GetStemFromName(string name)
        {
            if (SupportedStems.TryGetValue(name.ToLowerInvariant(), out var stem))
                return stem;

            return default;
        }

        public static SfxInfo GetSfxFromName(string name)
        {
            if (SoundEffects.TryGetValue(name.ToLowerInvariant(), out var sfx))
                return sfx;

            return default;
        }
    }
}
