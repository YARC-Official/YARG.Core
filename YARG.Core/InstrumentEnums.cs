using System;

namespace YARG.Core
{
    /// <summary>
    /// Available game modes.
    /// </summary>
    public enum GameMode
    {
        FiveFretGuitar,
        SixFretGuitar,

        FourLaneDrums,
        FiveLaneDrums,
        // TrueDrums,

        ProGuitar,
        ProKeys,

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

        SixFretGuitar,
        SixFretBass,
        SixFretRhythm,
        SixFretCoopGuitar,

        FourLaneDrums,
        ProDrums,

        FiveLaneDrums,

        // TrueDrums,

        ProGuitar_17Fret,
        ProGuitar_22Fret,
        ProBass_17Fret,
        ProBass_22Fret,

        ProKeys,

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

    /// <summary>
    /// Available difficulty levels.
    /// </summary>
    [Flags]
    public enum DifficultyMask : byte
    {
        None = 0,

        Easy   = 1 << 0,
        Medium = 1 << 1,
        Hard   = 1 << 2,
        Expert = 1 << 3,
        ExpertPlus = 1 << 4,

        All = Easy | Medium | Hard | Expert | ExpertPlus,
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

                Instrument.SixFretGuitar or
                Instrument.SixFretBass or
                Instrument.SixFretRhythm or
                Instrument.SixFretCoopGuitar => GameMode.SixFretGuitar,

                Instrument.FourLaneDrums or
                Instrument.ProDrums => GameMode.FourLaneDrums,

                Instrument.FiveLaneDrums => GameMode.FiveLaneDrums,

                // Instrument.TrueDrums => GameMode.TrueDrums,

                Instrument.ProGuitar_17Fret or
                Instrument.ProGuitar_22Fret or
                Instrument.ProBass_17Fret or
                Instrument.ProBass_22Fret => GameMode.ProGuitar,

                Instrument.ProKeys => GameMode.ProKeys,

                Instrument.Vocals or
                Instrument.Harmony => GameMode.Vocals,

                // Instrument.Dj => GameMode.Dj,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public static DifficultyMask ToDifficultyMask(this Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy       => DifficultyMask.Easy,
                Difficulty.Medium     => DifficultyMask.Easy,
                Difficulty.Hard       => DifficultyMask.Easy,
                Difficulty.Expert     => DifficultyMask.Expert,
                Difficulty.ExpertPlus => DifficultyMask.ExpertPlus,
                _ => throw new ArgumentException($"Invalid difficulty {difficulty}!")
            };
        }

        public static Difficulty ToDifficulty(this DifficultyMask difficulty)
        {
            return difficulty switch
            {
                DifficultyMask.Easy       => Difficulty.Easy,
                DifficultyMask.Medium     => Difficulty.Easy,
                DifficultyMask.Hard       => Difficulty.Easy,
                DifficultyMask.Expert     => Difficulty.Expert,
                DifficultyMask.ExpertPlus => Difficulty.ExpertPlus,
                _ => throw new ArgumentException($"Cannot convert difficulty mask {difficulty} into a single difficulty!")
            };
        }
    }
}