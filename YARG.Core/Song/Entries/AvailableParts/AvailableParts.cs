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
    public partial struct AvailableParts
    {
        public static readonly AvailableParts Default = new()
        {
            _bandDifficulty = PartValues.Default,
            _fiveFretGuitar = PartValues.Default,
            _fiveFretBass = PartValues.Default,
            _fiveFretRhythm = PartValues.Default,
            _fiveFretCoopGuitar = PartValues.Default,
            _keys = PartValues.Default,

            _sixFretGuitar = PartValues.Default,
            _sixFretBass = PartValues.Default,
            _sixFretRhythm = PartValues.Default,
            _sixFretCoopGuitar = PartValues.Default,

            _fourLaneDrums = PartValues.Default,
            _proDrums = PartValues.Default,
            _fiveLaneDrums = PartValues.Default,

            // _trueDrums = PartValues.Default,

            _proGuitar_17Fret = PartValues.Default,
            _proGuitar_22Fret = PartValues.Default,
            _proBass_17Fret = PartValues.Default,
            _proBass_22Fret = PartValues.Default,

            _proKeys = PartValues.Default,

            // _dj = PartValues.Default,

            _leadVocals = PartValues.Default,
            _harmonyVocals = PartValues.Default,
        };

        private PartValues _bandDifficulty;

        private PartValues _fiveFretGuitar;
        private PartValues _fiveFretBass;
        private PartValues _fiveFretRhythm;
        private PartValues _fiveFretCoopGuitar;
        private PartValues _keys;

        private PartValues _sixFretGuitar;
        private PartValues _sixFretBass;
        private PartValues _sixFretRhythm;
        private PartValues _sixFretCoopGuitar;

        private PartValues _fourLaneDrums;
        private PartValues _proDrums;
        private PartValues _fiveLaneDrums;

        // private PartValues _trueDrums;

        private PartValues _proGuitar_17Fret;
        private PartValues _proGuitar_22Fret;
        private PartValues _proBass_17Fret;
        private PartValues _proBass_22Fret;

        private PartValues _proKeys;

        // private PartValues _dj;

        private PartValues _leadVocals;
        private PartValues _harmonyVocals;
        private int _vocalsCount;

        public readonly int VocalsCount => _vocalsCount;

        public readonly sbyte BandDifficulty => _bandDifficulty.Intensity;

        public AvailableParts(BinaryReader reader)
        {
            PartValues Deserialize()
            {
                PartValues values = default;
                values.SubTracks = reader.ReadByte();
                values.Intensity = reader.ReadSByte();
                return values;
            }

            _bandDifficulty.Intensity = reader.ReadSByte();
            if (_bandDifficulty.Intensity != -1)
                _bandDifficulty.SubTracks = 1;

            _fiveFretGuitar = Deserialize();
            _fiveFretBass = Deserialize();
            _fiveFretRhythm = Deserialize();
            _fiveFretCoopGuitar = Deserialize();
            _keys = Deserialize();

            _sixFretGuitar = Deserialize();
            _sixFretBass = Deserialize();
            _sixFretRhythm = Deserialize();
            _sixFretCoopGuitar = Deserialize();

            _fourLaneDrums = Deserialize();
            _proDrums = Deserialize();
            _fiveLaneDrums = Deserialize();

            // _trueDrums = Deserialize();

            _proGuitar_17Fret = Deserialize();
            _proGuitar_22Fret = Deserialize();
            _proBass_17Fret = Deserialize();
            _proBass_22Fret = Deserialize();

            _proKeys = Deserialize();

            // _dj = Deserialize();

            _leadVocals = Deserialize();
            _harmonyVocals = Deserialize();
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
            SerializeValues(ref _fiveFretGuitar);
            SerializeValues(ref _fiveFretBass);
            SerializeValues(ref _fiveFretRhythm);
            SerializeValues(ref _fiveFretCoopGuitar);
            SerializeValues(ref _keys);

            SerializeValues(ref _sixFretGuitar);
            SerializeValues(ref _sixFretBass);
            SerializeValues(ref _sixFretRhythm);
            SerializeValues(ref _sixFretCoopGuitar);

            SerializeValues(ref _fourLaneDrums);
            SerializeValues(ref _proDrums);
            SerializeValues(ref _fiveLaneDrums);

            // SerializeValues(ref _trueDrums);

            SerializeValues(ref _proGuitar_17Fret);
            SerializeValues(ref _proGuitar_22Fret);
            SerializeValues(ref _proBass_17Fret);
            SerializeValues(ref _proBass_22Fret);

            SerializeValues(ref _proKeys);

            // SerializeValues(ref _dj);

            SerializeValues(ref _leadVocals);
            SerializeValues(ref _harmonyVocals);
        }

        public readonly bool CheckScanValidity()
        {
            return _fiveFretGuitar.SubTracks > 0 ||
                   _fiveFretBass.SubTracks > 0 ||
                   _fiveFretRhythm.SubTracks > 0 ||
                   _fiveFretCoopGuitar.SubTracks > 0 ||
                   _keys.SubTracks > 0 ||

                   _sixFretGuitar.SubTracks > 0 ||
                   _sixFretBass.SubTracks > 0 ||
                   _sixFretRhythm.SubTracks > 0 ||
                   _sixFretCoopGuitar.SubTracks > 0 ||

                   _fourLaneDrums.SubTracks > 0 ||
                   _proDrums.SubTracks > 0 ||
                   _fiveLaneDrums.SubTracks > 0 ||
                   //_trueDrums.subTracks > 0 ||
                   _proGuitar_17Fret.SubTracks > 0 ||
                   _proGuitar_22Fret.SubTracks > 0 ||
                   _proBass_17Fret.SubTracks > 0 ||
                   _proBass_22Fret.SubTracks > 0 ||

                   _proKeys.SubTracks > 0 ||

                   //_dj.subTracks > 0 ||

                   _leadVocals.SubTracks > 0 ||
                   _harmonyVocals.SubTracks > 0;
        }

        public readonly PartValues this[Instrument instrument]
        {
            get
            {
                return instrument switch
                {
                    Instrument.FiveFretGuitar => _fiveFretGuitar,
                    Instrument.FiveFretBass => _fiveFretBass,
                    Instrument.FiveFretRhythm => _fiveFretRhythm,
                    Instrument.FiveFretCoopGuitar => _fiveFretCoopGuitar,
                    Instrument.Keys => _keys,

                    Instrument.SixFretGuitar => _sixFretGuitar,
                    Instrument.SixFretBass => _sixFretBass,
                    Instrument.SixFretRhythm => _sixFretRhythm,
                    Instrument.SixFretCoopGuitar => _sixFretCoopGuitar,

                    Instrument.FourLaneDrums => _fourLaneDrums,
                    Instrument.FiveLaneDrums => _fiveLaneDrums,
                    Instrument.ProDrums => _proDrums,

                    // Instrument.TrueDrums => _trueDrums,

                    Instrument.ProGuitar_17Fret => _proGuitar_17Fret,
                    Instrument.ProGuitar_22Fret => _proGuitar_22Fret,
                    Instrument.ProBass_17Fret => _proBass_17Fret,
                    Instrument.ProBass_22Fret => _proBass_22Fret,

                    Instrument.ProKeys => _proKeys,

                    // Instrument.Dj => _dj,

                    Instrument.Vocals => _leadVocals,
                    Instrument.Harmony => _harmonyVocals,
                    Instrument.Band => _bandDifficulty,

                    _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
                };
            }
        }

        public readonly bool HasInstrument(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => _fiveFretGuitar.SubTracks > 0,
                Instrument.FiveFretBass => _fiveFretBass.SubTracks > 0,
                Instrument.FiveFretRhythm => _fiveFretRhythm.SubTracks > 0,
                Instrument.FiveFretCoopGuitar => _fiveFretCoopGuitar.SubTracks > 0,
                Instrument.Keys => _keys.SubTracks > 0,

                Instrument.SixFretGuitar => _sixFretGuitar.SubTracks > 0,
                Instrument.SixFretBass => _sixFretBass.SubTracks > 0,
                Instrument.SixFretRhythm => _sixFretRhythm.SubTracks > 0,
                Instrument.SixFretCoopGuitar => _sixFretCoopGuitar.SubTracks > 0,

                Instrument.FourLaneDrums => _fourLaneDrums.SubTracks > 0,
                Instrument.FiveLaneDrums => _fiveLaneDrums.SubTracks > 0,
                Instrument.ProDrums => _proDrums.SubTracks > 0,

                // Instrument.TrueDrums => _trueDrums.SubTracks > 0,

                Instrument.ProGuitar_17Fret => _proGuitar_17Fret.SubTracks > 0,
                Instrument.ProGuitar_22Fret => _proGuitar_22Fret.SubTracks > 0,
                Instrument.ProBass_17Fret => _proBass_17Fret.SubTracks > 0,
                Instrument.ProBass_22Fret => _proBass_22Fret.SubTracks > 0,

                Instrument.ProKeys => _proKeys.SubTracks > 0,

                // Instrument.Dj => _dj.SubTracks > 0,

                Instrument.Vocals => _leadVocals.SubTracks > 0,
                Instrument.Harmony => _harmonyVocals.SubTracks > 0,
                Instrument.Band => _bandDifficulty.SubTracks > 0,

                _ => false
            };
        }

        public readonly DrumsType GetDrumType()
        {
            if (_fourLaneDrums.SubTracks > 0)
                return DrumsType.FourLane;

            if (_fiveLaneDrums.SubTracks > 0)
                return DrumsType.FiveLane;

            return DrumsType.Unknown;
        }

        public void SetDrums(DrumPreparseHandler drums)
        {
            if (drums.Type == DrumsType.FiveLane)
                _fiveLaneDrums.Difficulties = drums.ValidatedDiffs;
            else
            {
                _fourLaneDrums.Difficulties = drums.ValidatedDiffs;
                if (drums.Type == DrumsType.ProDrums)
                    _proDrums.Difficulties = drums.ValidatedDiffs;
            }
        }

        private void SetVocalsCount()
        {
            if (_harmonyVocals[2])
                _vocalsCount = 3;
            else if (_harmonyVocals[1])
                _vocalsCount = 2;
            else if (_harmonyVocals[0] || _leadVocals[0])
                _vocalsCount = 1;
            else
                _vocalsCount = 0;
        }
    }
}