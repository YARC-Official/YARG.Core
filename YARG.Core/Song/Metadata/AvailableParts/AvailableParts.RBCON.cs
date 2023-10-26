using YARG.Core.IO;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        private static readonly int[] BandDiffMap =       { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap =     { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap =       { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap =       { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap =       { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap =     { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap =   { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap =  { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap =   { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap =    { 132, 175, 218, 279, 353, 427 };

        public void SetIntensities(RBCONDifficulties condiffs, YARGDTAReader reader)
        {
            int diff;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                diff = reader.ExtractInt32();
                switch (name)
                {
                    case "drum":
                    case "drums":
                        condiffs.FourLaneDrums = (short) diff;
                        SetRank(ref FourLaneDrums.intensity, diff, DrumDiffMap);
                        if (ProDrums.intensity == -1)
                            ProDrums.intensity = FourLaneDrums.intensity;
                        break;
                    case "guitar":
                        condiffs.FiveFretGuitar = (short) diff;
                        SetRank(ref FiveFretGuitar.intensity, diff, GuitarDiffMap);
                        break;
                    case "bass":
                        condiffs.FiveFretBass = (short) diff;
                        SetRank(ref FiveFretBass.intensity, diff, BassDiffMap);
                        break;
                    case "vocals":
                        condiffs.LeadVocals = (short) diff;
                        SetRank(ref LeadVocals.intensity, diff, VocalsDiffMap);
                        if (HarmonyVocals.intensity == -1)
                            HarmonyVocals.intensity = LeadVocals.intensity;
                        break;
                    case "keys":
                        condiffs.Keys = (short) diff;
                        SetRank(ref Keys.intensity, diff, KeysDiffMap);
                        break;
                    case "realGuitar":
                    case "real_guitar":
                        condiffs.ProGuitar = (short) diff;
                        SetRank(ref ProGuitar_17Fret.intensity, diff, RealGuitarDiffMap);
                        ProBass_22Fret.intensity = ProGuitar_17Fret.intensity;
                        break;
                    case "realBass":
                    case "real_bass":
                        condiffs.ProBass = (short) diff;
                        SetRank(ref ProBass_17Fret.intensity, diff, RealBassDiffMap);
                        ProBass_22Fret.intensity = ProBass_17Fret.intensity;
                        break;
                    case "realKeys":
                    case "real_keys":
                        condiffs.ProKeys = (short) diff;
                        SetRank(ref ProKeys.intensity, diff, RealKeysDiffMap);
                        break;
                    case "realDrums":
                    case "real_drums":
                        condiffs.ProDrums = (short) diff;
                        SetRank(ref ProDrums.intensity, diff, RealDrumsDiffMap);
                        if (FourLaneDrums.intensity == -1)
                            FourLaneDrums.intensity = ProDrums.intensity;
                        break;
                    case "harmVocals":
                    case "vocal_harm":
                        condiffs.HarmonyVocals = (short) diff;
                        SetRank(ref HarmonyVocals.intensity, diff, HarmonyDiffMap);
                        if (LeadVocals.intensity == -1)
                            LeadVocals.intensity = HarmonyVocals.intensity;
                        break;
                    case "band":
                        condiffs.band = (short) diff;
                        SetRank(ref _bandDifficulty.intensity, diff, BandDiffMap);
                        _bandDifficulty.subTracks = 1;
                        break;
                }
                reader.EndNode();
            }
        }

        private static void SetRank(ref sbyte intensity, int rank, int[] values)
        {
            sbyte i = 0;
            while (i < 6 && values[i] <= rank)
                ++i;
            intensity = i;
        }
    }
}
