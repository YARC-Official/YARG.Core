using System;
using YARG.Core;

namespace MoonscraperChartEditor.Song
{
    internal static class MoonExtensions
    {
        public static MoonChart.GameMode ToMoonGameMode(this GameMode mode) => mode switch
        {
            GameMode.FiveFretGuitar => MoonChart.GameMode.Guitar,
            GameMode.SixFretGuitar => MoonChart.GameMode.GHLGuitar,

            GameMode.FourLaneDrums => MoonChart.GameMode.Drums,
            GameMode.FiveLaneDrums => MoonChart.GameMode.Drums,

            GameMode.ProGuitar => MoonChart.GameMode.ProGuitar,
            GameMode.ProKeys => MoonChart.GameMode.ProKeys,

            GameMode.Vocals => MoonChart.GameMode.Vocals,

            _ => throw new NotImplementedException($"Unhandled game mode {mode}!")
        };

        public static MoonSong.MoonInstrument ToMoonInstrument(this Instrument instrument) => instrument switch
        {
            Instrument.FiveFretGuitar     => MoonSong.MoonInstrument.Guitar,
            Instrument.FiveFretCoopGuitar => MoonSong.MoonInstrument.GuitarCoop,
            Instrument.FiveFretBass       => MoonSong.MoonInstrument.Bass,
            Instrument.FiveFretRhythm     => MoonSong.MoonInstrument.Rhythm,
            Instrument.Keys               => MoonSong.MoonInstrument.Keys,

            Instrument.SixFretGuitar     => MoonSong.MoonInstrument.GHLiveGuitar,
            Instrument.SixFretCoopGuitar => MoonSong.MoonInstrument.GHLiveBass,
            Instrument.SixFretBass       => MoonSong.MoonInstrument.GHLiveRhythm,
            Instrument.SixFretRhythm     => MoonSong.MoonInstrument.GHLiveCoop,

            Instrument.FourLaneDrums or
            Instrument.FiveLaneDrums or
            Instrument.ProDrums => MoonSong.MoonInstrument.Drums,

            Instrument.ProGuitar_17Fret => MoonSong.MoonInstrument.ProGuitar_17Fret,
            Instrument.ProGuitar_22Fret => MoonSong.MoonInstrument.ProGuitar_22Fret,
            Instrument.ProBass_17Fret   => MoonSong.MoonInstrument.ProBass_17Fret,
            Instrument.ProBass_22Fret   => MoonSong.MoonInstrument.ProBass_22Fret,

            Instrument.ProKeys => MoonSong.MoonInstrument.ProKeys,

            // Vocals and harmony need to be handled specially
            // Instrument.Vocals  => MoonSong.MoonInstrument.Vocals,
            // Instrument.Harmony => MoonSong.MoonInstrument.Harmony1,

            _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
        };

        public static MoonSong.Difficulty ToMoonDifficulty(this Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy       => MoonSong.Difficulty.Easy,
            Difficulty.Medium     => MoonSong.Difficulty.Medium,
            Difficulty.Hard       => MoonSong.Difficulty.Hard,
            Difficulty.Expert or
            Difficulty.ExpertPlus => MoonSong.Difficulty.Expert,
            _ => throw new InvalidOperationException($"Invalid difficulty {difficulty}!")
        };
    }
}