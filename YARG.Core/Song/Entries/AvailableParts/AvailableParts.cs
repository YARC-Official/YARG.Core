using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    [Serializable]
    public sealed partial class AvailableParts
    {
        public sbyte BandDifficulty => _bandDifficulty.Intensity;
        public int VocalsCount { get; private set; }

        private PartValues _bandDifficulty;

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

        // private PartValues TrueDrums;

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
            _bandDifficulty = new(-1);
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

        public AvailableParts(BinaryReader reader)
        {
            PartValues DeserializeValues()
            {
                return new PartValues
                {
                    SubTracks = reader.ReadByte(),
                    Intensity = reader.ReadSByte()
                };
            }

            _bandDifficulty.Intensity = reader.ReadSByte();
            if (_bandDifficulty.Intensity != -1)
                _bandDifficulty.SubTracks = 1;

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
            SetVocalsCount();
        }

        public void Serialize(BinaryWriter writer)
        {
            void SerializeValues(ref PartValues values)
            {
                writer.Write(values.SubTracks);
                writer.Write(values.Intensity);
            }

            writer.Write(_bandDifficulty.Intensity);
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
            return FiveFretGuitar.SubTracks > 0 ||
                   FiveFretBass.SubTracks > 0 ||
                   FiveFretRhythm.SubTracks > 0 ||
                   FiveFretCoopGuitar.SubTracks > 0 ||
                   Keys.SubTracks > 0 ||

                   SixFretGuitar.SubTracks > 0 ||
                   SixFretBass.SubTracks > 0 ||
                   SixFretRhythm.SubTracks > 0 ||
                   SixFretCoopGuitar.SubTracks > 0 ||

                   FourLaneDrums.SubTracks > 0 ||
                   ProDrums.SubTracks > 0 ||
                   FiveLaneDrums.SubTracks > 0 ||
                   //TrueDrums.subTracks > 0 ||
                   ProGuitar_17Fret.SubTracks > 0 ||
                   ProGuitar_22Fret.SubTracks > 0 ||
                   ProBass_17Fret.SubTracks > 0 ||
                   ProBass_22Fret.SubTracks > 0 ||

                   ProKeys.SubTracks > 0 ||

                   //Dj.subTracks > 0 ||

                   LeadVocals.SubTracks > 0 ||
                   HarmonyVocals.SubTracks > 0;
        }

        private static readonly Instrument[] ALL_INSTRUMENTS = (Instrument[]) Enum.GetValues(typeof(Instrument));

        public Instrument[] GetInstruments()
        {
            return ALL_INSTRUMENTS
                .Where(instrument => HasInstrument(instrument))
                .ToArray();
        }

        public PartValues this[Instrument instrument]
        {
            get
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
                    Instrument.Band => _bandDifficulty,

                    _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
                };
            }
        }

        public bool HasInstrument(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar.SubTracks > 0,
                Instrument.FiveFretBass => FiveFretBass.SubTracks > 0,
                Instrument.FiveFretRhythm => FiveFretRhythm.SubTracks > 0,
                Instrument.FiveFretCoopGuitar => FiveFretCoopGuitar.SubTracks > 0,
                Instrument.Keys => Keys.SubTracks > 0,

                Instrument.SixFretGuitar => SixFretGuitar.SubTracks > 0,
                Instrument.SixFretBass => SixFretBass.SubTracks > 0,
                Instrument.SixFretRhythm => SixFretRhythm.SubTracks > 0,
                Instrument.SixFretCoopGuitar => SixFretCoopGuitar.SubTracks > 0,

                Instrument.FourLaneDrums => FourLaneDrums.SubTracks > 0,
                Instrument.FiveLaneDrums => FiveLaneDrums.SubTracks > 0,
                Instrument.ProDrums => ProDrums.SubTracks > 0,

                // Instrument.TrueDrums => TrueDrums.SubTracks > 0,

                Instrument.ProGuitar_17Fret => ProGuitar_17Fret.SubTracks > 0,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret.SubTracks > 0,
                Instrument.ProBass_17Fret => ProBass_17Fret.SubTracks > 0,
                Instrument.ProBass_22Fret => ProBass_22Fret.SubTracks > 0,

                Instrument.ProKeys => ProKeys.SubTracks > 0,

                // Instrument.Dj => Dj.SubTracks > 0,

                Instrument.Vocals => LeadVocals.SubTracks > 0,
                Instrument.Harmony => HarmonyVocals.SubTracks > 0,
                Instrument.Band => _bandDifficulty.SubTracks > 0,

                _ => false
            };
        }

        public DrumsType GetDrumType()
        {
            if (FourLaneDrums.SubTracks > 0)
                return DrumsType.FourLane;

            if (FiveLaneDrums.SubTracks > 0)
                return DrumsType.FiveLane;

            return DrumsType.Unknown;
        }

        public void SetDrums(DrumPreparseHandler drums)
        {
            if (drums.Type == DrumsType.FiveLane)
                FiveLaneDrums.Difficulties = drums.ValidatedDiffs;
            else
            {
                FourLaneDrums.Difficulties = drums.ValidatedDiffs;
                if (drums.Type == DrumsType.ProDrums)
                    ProDrums.Difficulties = drums.ValidatedDiffs;
            }
        }

        private void SetVocalsCount()
        {
            if (HarmonyVocals[2])
                VocalsCount = 3;
            else if (HarmonyVocals[1])
                VocalsCount = 2;
            else if (HarmonyVocals[0] || LeadVocals[0])
                VocalsCount = 1;
            else
                VocalsCount = 0;
        }
    }
}