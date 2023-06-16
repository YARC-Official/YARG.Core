using System;

namespace YARG.Core
{
    /// <summary>
    /// Available game modes.
    /// </summary>
    public enum GameMode
    {
        FiveFretGuitar,
        // SixFretGuitar,

        FourLaneDrums,
        FiveLaneDrums,
        // TrueDrums,

        ProGuitar,
        // ProKeys,

        Vocals,

        // Dj,
    }

    /// <summary>
    /// Available instruments.
    /// </summary>
    public enum Instrument
    {
        FiveFretGuitar,
        FiveFretBass,
        FiveFretRhythm,
        FiveFretCoopGuitar,
        Keys,

        // SixFretGuitar,
        // SixFretBass,
        // SixFretRhythm,
        // SixFretCoopGuitar,

        FourLaneDrums,
        ProDrums,

        FiveLaneDrums,

        // TrueDrums,

        ProGuitar_17Fret,
        ProGuitar_22Fret,
        ProBass_17Fret,
        ProBass_22Fret,

        // ProKeys,

        Vocals,
        Harmony,

        // Dj,
    }

    /// <summary>
    /// Available difficulty levels.
    /// </summary>
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        Expert,
        ExpertPlus,
    }

    public static class ChartEnumExtensions
    {
        public static GameMode ToGameMode(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or
                Instrument.FiveFretBass or
                Instrument.FiveFretRhythm or
                Instrument.FiveFretCoopGuitar or
                Instrument.Keys => GameMode.FiveFretGuitar,

                // Instrument.SixFretGuitar or
                // Instrument.SixFretBass or
                // Instrument.SixFretRhythm or
                // Instrument.SixFretCoopGuitar => GameMode.SixFretGuitar,

                Instrument.FourLaneDrums or
                Instrument.ProDrums => GameMode.FourLaneDrums,

                Instrument.FiveLaneDrums => GameMode.FiveLaneDrums,

                // Instrument.TrueDrums => GameMode.TrueDrums,

                Instrument.ProGuitar_17Fret or
                Instrument.ProGuitar_22Fret or
                Instrument.ProBass_17Fret or
                Instrument.ProBass_22Fret => GameMode.ProGuitar,

                // Instrument.ProKeys => GameMode.ProKeys,

                Instrument.Vocals or
                Instrument.Harmony => GameMode.Vocals,

                // Instrument.Dj => GameMode.Dj,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }
    }
}