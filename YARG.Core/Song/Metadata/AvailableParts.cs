using System;
using System.IO;
using YARG.Core.Deserialization;

namespace YARG.Core.Song
{
    [Serializable]
    public class AvailableParts
    {
        public PartValues FiveFretGuitar;
        public PartValues FiveFretBass;
        public PartValues FiveFretRhythm;
        public PartValues FiveFretCoopGuitar;
        public PartValues Keys;

        public PartValues SixFretGuitar;
        public PartValues SixFretBass;
        public PartValues SixFretRhythm;
        public PartValues SixFretCoopGuitar;

        public PartValues FourLaneDrums;
        public PartValues ProDrums;
        public PartValues FiveLaneDrums;

        // public PartValues TrueDrums;

        public PartValues ProGuitar_17Fret;
        public PartValues ProGuitar_22Fret;
        public PartValues ProBass_17Fret;
        public PartValues ProBass_22Fret;

        public PartValues ProKeys;

        // public PartValues Dj;

        public PartValues LeadVocals;
        public PartValues HarmonyVocals;

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
    }
}