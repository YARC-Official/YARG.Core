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
            band = reader.ReadInt16();
            FiveFretGuitar = reader.ReadInt16();
            FiveFretBass = reader.ReadInt16();
            FiveFretRhythm = reader.ReadInt16();
            FiveFretCoop = reader.ReadInt16();
            Keys = reader.ReadInt16();
            FourLaneDrums = reader.ReadInt16();
            ProDrums = reader.ReadInt16();
            ProGuitar = reader.ReadInt16();
            ProBass = reader.ReadInt16();
            ProKeys = reader.ReadInt16();
            LeadVocals = reader.ReadInt16();
            HarmonyVocals = reader.ReadInt16();
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
