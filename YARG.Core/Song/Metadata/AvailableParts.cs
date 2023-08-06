using System;
using System.IO;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Deserialization.Ini;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    [Serializable]
    public class AvailableParts
    {

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

        /// <summary>
        /// Uses the current instrument to institute applicable test parameters.
        /// This does not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public DrumType ParseChart(YARGChartFileReader reader, DrumType drumType)
        {
            DrumPreparseHandler drums = new(drumType);
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument())
                    reader.SkipTrack();
                else if (reader.Instrument != NoteTracks_Chart.Drums)
                    ParseChartTrack(reader);
                else
                    drums.ParseChart(reader);
            }

            if (drums.Type == DrumType.FIVE_LANE)
                FiveLaneDrums.subTracks = drums.ValidatedDiffs;
            else
            {
                FourLaneDrums.subTracks = drums.ValidatedDiffs;
                if (drums.Type == DrumType.FOUR_PRO)
                    ProDrums.subTracks = drums.ValidatedDiffs;
            }
            return drums.Type;
            
        }

        private void ParseChartTrack(YARGChartFileReader reader)
        {
            bool skip = reader.Instrument switch
            {
                NoteTracks_Chart.Single => ChartPreparser.Preparse(reader, ref FiveFretGuitar, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleBass => ChartPreparser.Preparse(reader, ref FiveFretBass, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleRhythm => ChartPreparser.Preparse(reader, ref FiveFretRhythm, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleGuitar => ChartPreparser.Preparse(reader, ref FiveFretCoopGuitar, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.GHLGuitar => ChartPreparser.Preparse(reader, ref SixFretGuitar, ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLBass => ChartPreparser.Preparse(reader, ref SixFretBass, ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLRhythm => ChartPreparser.Preparse(reader, ref SixFretRhythm, ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLCoop => ChartPreparser.Preparse(reader, ref SixFretCoopGuitar, ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.Keys => ChartPreparser.Preparse(reader, ref Keys, ChartPreparser.ValidateKeys),
                _ => true,
            };

            if (skip)
                reader.SkipTrack();
        }

        /// <summary>
        /// This not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public DrumType ParseMidi(YARGFile file, DrumType drumType)
        {
            YARGMidiReader reader = new(file);
            DrumPreparseHandler drums = new(drumType);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1 || reader.GetEvent().type != MidiEventType.Text_TrackName)
                    continue;

                string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                if (YARGMidiReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events && type != MidiTrackType.Beat)
                {
                    if (type != MidiTrackType.Drums)
                    {
                        switch (type)
                        {
                            case MidiTrackType.Guitar_5:      if (!FiveFretGuitar.WasParsed())     FiveFretGuitar.subTracks     |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Bass_5:        if (!FiveFretBass.WasParsed())       FiveFretBass.subTracks       |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Rhythm_5:      if (!FiveFretRhythm.WasParsed())     FiveFretRhythm.subTracks     |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Coop_5:        if (!FiveFretCoopGuitar.WasParsed()) FiveFretCoopGuitar.subTracks |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Guitar_6:      if (!SixFretGuitar.WasParsed())      SixFretGuitar.subTracks      |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Bass_6:        if (!SixFretBass.WasParsed())        SixFretBass.subTracks        |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Rhythm_6:      if (!SixFretRhythm.WasParsed())      SixFretRhythm.subTracks      |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Coop_6:        if (!SixFretCoopGuitar.WasParsed())  SixFretCoopGuitar.subTracks  |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Pro_Guitar_17: if (!ProGuitar_17Fret.WasParsed())   ProGuitar_17Fret.subTracks   |= MidiInstrumentPreparser.Parse<Midi_ProGuitar17>(reader); break;
                            case MidiTrackType.Pro_Guitar_22: if (!ProGuitar_22Fret.WasParsed())   ProGuitar_22Fret.subTracks   |= MidiInstrumentPreparser.Parse<Midi_ProGuitar22>(reader); break;
                            case MidiTrackType.Pro_Bass_17:   if (!ProBass_17Fret.WasParsed())     ProBass_17Fret.subTracks     |= MidiInstrumentPreparser.Parse<Midi_ProGuitar17>(reader); break;
                            case MidiTrackType.Pro_Bass_22:   if (!ProBass_22Fret.WasParsed())     ProBass_22Fret.subTracks     |= MidiInstrumentPreparser.Parse<Midi_ProGuitar22>(reader); break;
                            case MidiTrackType.Keys:          if (!Keys.WasParsed())               Keys.subTracks               |= MidiInstrumentPreparser.Parse<Midi_Keys>(reader); break;
                            case MidiTrackType.Pro_Keys_X:    if (!ProKeys[3])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysX>(reader); break;
                            case MidiTrackType.Pro_Keys_H:    if (!ProKeys[2])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysH>(reader); break;
                            case MidiTrackType.Pro_Keys_M:    if (!ProKeys[1])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysM>(reader); break;
                            case MidiTrackType.Pro_Keys_E:    if (!ProKeys[0])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysE>(reader); break;

                            case MidiTrackType.Vocals:        if (!LeadVocals[0]    && MidiPreparser.Parse<Midi_Vocal>(reader))   LeadVocals.Set(0); break;
                            case MidiTrackType.Harm1:         if (!HarmonyVocals[0] && MidiPreparser.Parse<Midi_Vocal>(reader))   HarmonyVocals.Set(0); break;
                            case MidiTrackType.Harm2:         if (!HarmonyVocals[1] && MidiPreparser.Parse<Midi_Harmony>(reader)) HarmonyVocals.Set(1); break;
                            case MidiTrackType.Harm3:         if (!HarmonyVocals[2] && MidiPreparser.Parse<Midi_Harmony>(reader)) HarmonyVocals.Set(2); break;
                        }
                    }
                    else
                        drums.ParseMidi(reader);
                }
            }

            if (drums.Type == DrumType.FIVE_LANE)
                FiveLaneDrums.subTracks = drums.ValidatedDiffs;
            else
            {
                FourLaneDrums.subTracks = drums.ValidatedDiffs;
                if (drums.Type == DrumType.FOUR_PRO)
                    ProDrums.subTracks = drums.ValidatedDiffs;
            }
            return drums.Type;
        }

        public void SetIntensities(IniSection modifiers)
        {
            if (modifiers.TryGet("diff_guitar", out int intensity))
                FiveFretGuitar.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_bass", out intensity))
                FiveFretBass.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_rhythm", out intensity))
                FiveFretRhythm.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_guitar_coop", out intensity))
                FiveFretCoopGuitar.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_guitarghl", out intensity))
                SixFretGuitar.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_bassghl", out intensity))
                SixFretBass.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_rhythm_ghl", out intensity))
                SixFretRhythm.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_guitar_coop_ghl", out intensity))
                SixFretCoopGuitar.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_keys", out intensity))
                Keys.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_drums", out intensity))
            {
                FourLaneDrums.intensity = (sbyte) intensity;
                ProDrums.intensity = (sbyte) intensity;
                FiveLaneDrums.intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums_real", out intensity))
            {
                ProDrums.intensity = (sbyte) intensity;
                if (FourLaneDrums.intensity == -1)
                    FourLaneDrums.intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity))
                ProGuitar_17Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_bass_real", out intensity))
                ProBass_17Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_guitar_real_22", out intensity))
                ProGuitar_22Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_bass_real_22", out intensity))
                ProBass_22Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_vocals", out intensity))
                LeadVocals.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_vocals_harm", out intensity))
                HarmonyVocals.intensity = (sbyte) intensity;
        }
    }
}