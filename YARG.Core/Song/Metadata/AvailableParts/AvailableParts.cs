using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    [Serializable]
    public sealed partial class AvailableParts
    {
        public sbyte BandDifficulty => _bandDifficulty;

        private sbyte _bandDifficulty;

        private PartValues FiveFretGuitar;
        private PartValues FiveFretBass;
        private PartValues FiveFretRhythm;
        private PartValues FiveFretCoopGuitar;
        private PartValues Keys;

        private PartValues SixFretGuitar;
        private PartValues SixFretBass;
        private PartValues SixFretRhythm;
        private PartValues SixFretCoopGuitar;

        private PartValues FourLaneDrums;
        private PartValues ProDrums;
        private PartValues FiveLaneDrums;

        // puprivateblic PartValues TrueDrums;

        private PartValues ProGuitar_17Fret;
        private PartValues ProGuitar_22Fret;
        private PartValues ProBass_17Fret;
        private PartValues ProBass_22Fret;

        private PartValues ProKeys;

        // private PartValues Dj;

        private PartValues LeadVocals;
        private PartValues HarmonyVocals;

        public AvailableParts()
        {
            _bandDifficulty = -1;
            FiveFretGuitar = new(-1);
            FiveFretBass = new(-1);
            FiveFretRhythm = new(-1);
            FiveFretCoopGuitar = new(-1);
            Keys = new(-1);

            SixFretGuitar = new(-1);
            SixFretBass = new(-1);
            SixFretRhythm = new(-1);
            SixFretCoopGuitar = new(-1);

            FourLaneDrums = new(-1);
            ProDrums = new(-1);
            FiveLaneDrums = new(-1);

            // TrueDrums = new(-1);

            ProGuitar_17Fret = new(-1);
            ProGuitar_22Fret = new(-1);
            ProBass_17Fret = new(-1);
            ProBass_22Fret = new(-1);

            ProKeys = new(-1);

            // Dj = new(-1);

            LeadVocals = new(-1);
            HarmonyVocals = new(-1);
        }

        public AvailableParts(YARGBinaryReader reader)
        {
            PartValues DeserializeValues()
            {
                return new PartValues
                {
                    subTracks = reader.ReadByte(),
                    intensity = reader.ReadSByte()
                };
            }

            _bandDifficulty = reader.ReadSByte();
            FiveFretGuitar = DeserializeValues();
            FiveFretBass = DeserializeValues();
            FiveFretRhythm = DeserializeValues();
            FiveFretCoopGuitar = DeserializeValues();
            Keys = DeserializeValues();

            SixFretGuitar = DeserializeValues();
            SixFretBass = DeserializeValues();
            SixFretRhythm = DeserializeValues();
            SixFretCoopGuitar = DeserializeValues();

            FourLaneDrums = DeserializeValues();
            ProDrums = DeserializeValues();
            FiveLaneDrums = DeserializeValues();

            // TrueDrums = DeserializeValues();

            ProGuitar_17Fret = DeserializeValues();
            ProGuitar_22Fret = DeserializeValues();
            ProBass_17Fret = DeserializeValues();
            ProBass_22Fret = DeserializeValues();
    
            ProKeys = DeserializeValues();

            // Dj = DeserializeValues();

            LeadVocals = DeserializeValues();
            HarmonyVocals = DeserializeValues();
        }

        public void Serialize(BinaryWriter writer)
        {
            void SerializeValues(ref PartValues values)
            {
                writer.Write(values.subTracks);
                writer.Write(values.intensity);
            }

            writer.Write(BandDifficulty);
            SerializeValues(ref FiveFretGuitar);
            SerializeValues(ref FiveFretBass);
            SerializeValues(ref FiveFretRhythm);
            SerializeValues(ref FiveFretCoopGuitar);
            SerializeValues(ref Keys);

            SerializeValues(ref SixFretGuitar);
            SerializeValues(ref SixFretBass);
            SerializeValues(ref SixFretRhythm);
            SerializeValues(ref SixFretCoopGuitar);

            SerializeValues(ref FourLaneDrums);
            SerializeValues(ref ProDrums);
            SerializeValues(ref FiveLaneDrums);

            // SerializeValues(TrueDrums);

            SerializeValues(ref ProGuitar_17Fret);
            SerializeValues(ref ProGuitar_22Fret);
            SerializeValues(ref ProBass_17Fret);
            SerializeValues(ref ProBass_22Fret);

            SerializeValues(ref ProKeys);

            // SerializeValues(Dj);

            SerializeValues(ref LeadVocals);
            SerializeValues(ref HarmonyVocals);
        }

        public bool CheckScanValidity()
        {
            return FiveFretGuitar.subTracks > 0 ||
                   FiveFretBass.subTracks > 0 ||
                   FiveFretRhythm.subTracks > 0 ||
                   FiveFretCoopGuitar.subTracks > 0 ||
                   Keys.subTracks > 0 ||

                   SixFretGuitar.subTracks > 0 ||
                   SixFretBass.subTracks > 0 ||
                   SixFretRhythm.subTracks > 0 ||
                   SixFretCoopGuitar.subTracks > 0 ||

                   FourLaneDrums.subTracks > 0 ||
                   ProDrums.subTracks > 0 ||
                   FiveLaneDrums.subTracks > 0 ||
                   //TrueDrums.subTracks > 0 ||
                   ProGuitar_17Fret.subTracks > 0 ||
                   ProGuitar_22Fret.subTracks > 0 ||
                   ProBass_17Fret.subTracks > 0 ||
                   ProBass_22Fret.subTracks > 0 ||

                   ProKeys.subTracks > 0 ||

                   //Dj.subTracks > 0 ||

                   LeadVocals.subTracks > 0 ||
                   HarmonyVocals.subTracks > 0;
        }

        private static readonly Instrument[] ALL_INSTRUMENTS = (Instrument[]) Enum.GetValues(typeof(Instrument));

        public List<Instrument> GetInstruments()
        {
            return ALL_INSTRUMENTS.Where(instrument => HasInstrument(instrument)).ToList();
        }

        public PartValues GetValues(Instrument instrument)
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

                // Instrument.TrueDrums => TrueDrums,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret,
                Instrument.ProBass_17Fret => ProBass_17Fret,
                Instrument.ProBass_22Fret => ProBass_22Fret,

                Instrument.ProKeys => ProKeys,

                // Instrument.Dj => Dj,

                Instrument.Vocals => LeadVocals,
                Instrument.Harmony => HarmonyVocals,

                _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
            };
        }

        public bool HasInstrument(Instrument instrument)
        {
            try
            {
                return GetValues(instrument).subTracks > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool HasPart(Instrument instrument, int subtrack)
        {
            try
            {
                return GetValues(instrument)[subtrack];
            }
            catch
            {
                return false;
            }
        }

        public DrumsType GetDrumType()
        {
            if (FourLaneDrums.subTracks > 0)
                return DrumsType.FourLane;

            if (FiveLaneDrums.subTracks > 0)
                return DrumsType.FiveLane;

            return DrumsType.Unknown;
        }
    }
}