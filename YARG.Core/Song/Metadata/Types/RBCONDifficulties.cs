using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    [Serializable]
    public sealed class RBCONDifficulties
    {
        public short band = -1;
        public short FiveFretGuitar = -1;
        public short FiveFretBass = -1;
        public short FiveFretRhythm = -1;
        public short FiveFretCoop = -1;
        public short Keys = -1;
        public short FourLaneDrums = -1;
        public short ProDrums = -1;
        public short ProGuitar = -1;
        public short ProBass = -1;
        public short ProKeys = -1;
        public short LeadVocals = -1;
        public short HarmonyVocals = -1;

        public RBCONDifficulties() { }
        public RBCONDifficulties(BinaryReader reader)
        {
            band = reader.Read<short>(Endianness.Little);
            FiveFretGuitar = reader.Read<short>(Endianness.Little);
            FiveFretBass = reader.Read<short>(Endianness.Little);
            FiveFretRhythm = reader.Read<short>(Endianness.Little);
            FiveFretCoop = reader.Read<short>(Endianness.Little);
            Keys = reader.Read<short>(Endianness.Little);
            FourLaneDrums = reader.Read<short>(Endianness.Little);
            ProDrums = reader.Read<short>(Endianness.Little);
            ProGuitar = reader.Read<short>(Endianness.Little);
            ProBass = reader.Read<short>(Endianness.Little);
            ProKeys = reader.Read<short>(Endianness.Little);
            LeadVocals = reader.Read<short>(Endianness.Little);
            HarmonyVocals = reader.Read<short>(Endianness.Little);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(band);
            writer.Write(FiveFretGuitar);
            writer.Write(FiveFretBass);
            writer.Write(FiveFretRhythm);
            writer.Write(FiveFretCoop);
            writer.Write(Keys);
            writer.Write(FourLaneDrums);
            writer.Write(ProDrums);
            writer.Write(ProGuitar);
            writer.Write(ProBass);
            writer.Write(ProKeys);
            writer.Write(LeadVocals);
            writer.Write(HarmonyVocals);
        }
    }


}
