using System;

namespace YARG.Core.Chart
{
    public class AvailableParts
    {
        public DifficultyMask FiveFretGuitar;
        public DifficultyMask FiveFretBass;
        public DifficultyMask FiveFretRhythm;
        public DifficultyMask FiveFretCoopGuitar;
        public DifficultyMask Keys;

        public DifficultyMask SixFretGuitar;
        public DifficultyMask SixFretBass;
        public DifficultyMask SixFretRhythm;
        public DifficultyMask SixFretCoopGuitar;

        public DifficultyMask FourLaneDrums;
        public DifficultyMask ProDrums;
        public DifficultyMask FiveLaneDrums;

        public DifficultyMask ProGuitar_17Fret;
        public DifficultyMask ProGuitar_22Fret;
        public DifficultyMask ProBass_17Fret;
        public DifficultyMask ProBass_22Fret;

        public bool VocalsAvailable;
        public bool HarmonyAvailable;

        public DifficultyMask GetAvailableDifficulties(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar,
                Instrument.FiveFretBass => FiveFretBass,
                Instrument.FiveFretRhythm => FiveFretRhythm,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar,
                Instrument.Keys => Keys,

                Instrument.SixFretGuitar => SixFretGuitar,
                Instrument.SixFretBass => SixFretBass,
                Instrument.SixFretRhythm => SixFretRhythm,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar,

                Instrument.FourLaneDrums => FourLaneDrums,
                Instrument.FiveLaneDrums => FiveLaneDrums,
                Instrument.ProDrums => ProDrums,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret,
                Instrument.ProBass_17Fret => ProBass_17Fret,
                Instrument.ProBass_22Fret => ProBass_22Fret,

                Instrument.Vocals or
                Instrument.Harmony => throw new ArgumentException($"{instrument} does not have difficulties!", nameof(instrument)),

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public void SetAvailableDifficulties(Instrument instrument, DifficultyMask difficulties)
        {
            _ = instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar = difficulties,
                Instrument.FiveFretBass => FiveFretBass = difficulties,
                Instrument.FiveFretRhythm => FiveFretRhythm = difficulties,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar = difficulties,
                Instrument.Keys => Keys = difficulties,

                Instrument.SixFretGuitar => SixFretGuitar = difficulties,
                Instrument.SixFretBass => SixFretBass = difficulties,
                Instrument.SixFretRhythm => SixFretRhythm = difficulties,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar = difficulties,

                Instrument.FourLaneDrums => FourLaneDrums = difficulties,
                Instrument.FiveLaneDrums => FiveLaneDrums = difficulties,
                Instrument.ProDrums => ProDrums = difficulties,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret = difficulties,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret = difficulties,
                Instrument.ProBass_17Fret => ProBass_17Fret = difficulties,
                Instrument.ProBass_22Fret => ProBass_22Fret = difficulties,

                Instrument.Vocals or
                Instrument.Harmony => throw new ArgumentException($"{instrument} does not have difficulties!", nameof(instrument)),

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public void AddAvailableDifficulty(Instrument instrument, DifficultyMask difficulty)
        {
            // Hack to use a switch expression as a statement
            _ = instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar |= difficulty,
                Instrument.FiveFretBass => FiveFretBass |= difficulty,
                Instrument.FiveFretRhythm => FiveFretRhythm |= difficulty,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar |= difficulty,
                Instrument.Keys => Keys |= difficulty,

                Instrument.SixFretGuitar => SixFretGuitar |= difficulty,
                Instrument.SixFretBass => SixFretBass |= difficulty,
                Instrument.SixFretRhythm => SixFretRhythm |= difficulty,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar |= difficulty,

                Instrument.FourLaneDrums => FourLaneDrums |= difficulty,
                Instrument.FiveLaneDrums => FiveLaneDrums |= difficulty,
                Instrument.ProDrums => ProDrums |= difficulty,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret |= difficulty,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret |= difficulty,
                Instrument.ProBass_17Fret => ProBass_17Fret |= difficulty,
                Instrument.ProBass_22Fret => ProBass_22Fret |= difficulty,

                Instrument.Vocals or
                Instrument.Harmony => throw new ArgumentException($"{instrument} does not have difficulties!", nameof(instrument)),

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public void RemoveAvailableDifficulty(Instrument instrument, DifficultyMask difficulty)
        {
            // Hack to use a switch expression as a statement
            _ = instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar &= ~difficulty,
                Instrument.FiveFretBass => FiveFretBass &= ~difficulty,
                Instrument.FiveFretRhythm => FiveFretRhythm &= ~difficulty,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar &= ~difficulty,
                Instrument.Keys => Keys &= ~difficulty,

                Instrument.SixFretGuitar => SixFretGuitar &= ~difficulty,
                Instrument.SixFretBass => SixFretBass &= ~difficulty,
                Instrument.SixFretRhythm => SixFretRhythm &= ~difficulty,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar &= ~difficulty,

                Instrument.FourLaneDrums => FourLaneDrums &= ~difficulty,
                Instrument.FiveLaneDrums => FiveLaneDrums &= ~difficulty,
                Instrument.ProDrums => ProDrums &= ~difficulty,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret &= ~difficulty,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret &= ~difficulty,
                Instrument.ProBass_17Fret => ProBass_17Fret &= ~difficulty,
                Instrument.ProBass_22Fret => ProBass_22Fret &= ~difficulty,

                Instrument.Vocals or
                Instrument.Harmony => throw new ArgumentException($"{instrument} does not have difficulties!", nameof(instrument)),

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public bool IsInstrumentAvailable(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar != DifficultyMask.None,
                Instrument.FiveFretBass => FiveFretBass != DifficultyMask.None,
                Instrument.FiveFretRhythm => FiveFretRhythm != DifficultyMask.None,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar != DifficultyMask.None,
                Instrument.Keys => Keys != DifficultyMask.None,

                Instrument.SixFretGuitar => SixFretGuitar != DifficultyMask.None,
                Instrument.SixFretBass => SixFretBass != DifficultyMask.None,
                Instrument.SixFretRhythm => SixFretRhythm != DifficultyMask.None,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar != DifficultyMask.None,

                Instrument.FourLaneDrums => FourLaneDrums != DifficultyMask.None,
                Instrument.FiveLaneDrums => FiveLaneDrums != DifficultyMask.None,
                Instrument.ProDrums => ProDrums != DifficultyMask.None,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret != DifficultyMask.None,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret != DifficultyMask.None,
                Instrument.ProBass_17Fret => ProBass_17Fret != DifficultyMask.None,
                Instrument.ProBass_22Fret => ProBass_22Fret != DifficultyMask.None,

                Instrument.Vocals => VocalsAvailable,
                Instrument.Harmony => HarmonyAvailable,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public void SetInstrumentAvailable(Instrument instrument, bool available)
        {
            static bool SetDifficulties(ref DifficultyMask field, bool available)
            {
                field = available ? DifficultyMask.All : DifficultyMask.None;
                return available;
            }

            _ = instrument switch
            {
                Instrument.FiveFretGuitar => SetDifficulties(ref FiveFretGuitar, available),
                Instrument.FiveFretBass => SetDifficulties(ref FiveFretBass, available),
                Instrument.FiveFretRhythm => SetDifficulties(ref FiveFretRhythm, available),
                Instrument.FiveFretCoopGuitar => SetDifficulties(ref FiveFretCoopGuitar, available),
                Instrument.Keys => SetDifficulties(ref Keys, available),

                Instrument.SixFretGuitar => SetDifficulties(ref SixFretGuitar, available),
                Instrument.SixFretBass => SetDifficulties(ref SixFretBass, available),
                Instrument.SixFretRhythm => SetDifficulties(ref SixFretRhythm, available),
                Instrument.SixFretCoopGuitar => SetDifficulties(ref SixFretCoopGuitar, available),

                Instrument.FourLaneDrums => SetDifficulties(ref FourLaneDrums, available),
                Instrument.FiveLaneDrums => SetDifficulties(ref FiveLaneDrums, available),
                Instrument.ProDrums => SetDifficulties(ref ProDrums, available),

                Instrument.ProGuitar_17Fret => SetDifficulties(ref ProGuitar_17Fret, available),
                Instrument.ProGuitar_22Fret => SetDifficulties(ref ProGuitar_22Fret, available),
                Instrument.ProBass_17Fret => SetDifficulties(ref ProBass_17Fret, available),
                Instrument.ProBass_22Fret => SetDifficulties(ref ProBass_22Fret, available),

                Instrument.Vocals => VocalsAvailable = available,
                Instrument.Harmony => HarmonyAvailable = available,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }
    }
}