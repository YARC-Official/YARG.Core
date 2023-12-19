using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        public RBCONDifficulties(YARGBinaryReader reader)
        {
            band = reader.Read<short>();
            FiveFretGuitar = reader.Read<short>();
            FiveFretBass = reader.Read<short>();
            FiveFretRhythm = reader.Read<short>();
            FiveFretCoop = reader.Read<short>();
            Keys = reader.Read<short>();
            FourLaneDrums = reader.Read<short>();
            ProDrums = reader.Read<short>();
            ProGuitar = reader.Read<short>();
            ProBass = reader.Read<short>();
            ProKeys = reader.Read<short>();
            LeadVocals = reader.Read<short>();
            HarmonyVocals = reader.Read<short>();
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
