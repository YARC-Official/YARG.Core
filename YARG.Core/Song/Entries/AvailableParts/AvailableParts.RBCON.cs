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
                        SetRank(ref FourLaneDrums.Intensity, diff, DrumDiffMap);
                        if (ProDrums.Intensity == -1)
                            ProDrums.Intensity = FourLaneDrums.Intensity;
                        break;
                    case "guitar":
                        condiffs.FiveFretGuitar = (short) diff;
                        SetRank(ref FiveFretGuitar.Intensity, diff, GuitarDiffMap);
                        break;
                    case "bass":
                        condiffs.FiveFretBass = (short) diff;
                        SetRank(ref FiveFretBass.Intensity, diff, BassDiffMap);
                        break;
                    case "vocals":
                        condiffs.LeadVocals = (short) diff;
                        SetRank(ref LeadVocals.Intensity, diff, VocalsDiffMap);
                        if (HarmonyVocals.Intensity == -1)
                            HarmonyVocals.Intensity = LeadVocals.Intensity;
                        break;
                    case "keys":
                        condiffs.Keys = (short) diff;
                        SetRank(ref Keys.Intensity, diff, KeysDiffMap);
                        break;
                    case "realGuitar":
                    case "real_guitar":
                        condiffs.ProGuitar = (short) diff;
                        SetRank(ref ProGuitar_17Fret.Intensity, diff, RealGuitarDiffMap);
                        ProBass_22Fret.Intensity = ProGuitar_17Fret.Intensity;
                        break;
                    case "realBass":
                    case "real_bass":
                        condiffs.ProBass = (short) diff;
                        SetRank(ref ProBass_17Fret.Intensity, diff, RealBassDiffMap);
                        ProBass_22Fret.Intensity = ProBass_17Fret.Intensity;
                        break;
                    case "realKeys":
                    case "real_keys":
                        condiffs.ProKeys = (short) diff;
                        SetRank(ref ProKeys.Intensity, diff, RealKeysDiffMap);
                        break;
                    case "realDrums":
                    case "real_drums":
                        condiffs.ProDrums = (short) diff;
                        SetRank(ref ProDrums.Intensity, diff, RealDrumsDiffMap);
                        if (FourLaneDrums.Intensity == -1)
                            FourLaneDrums.Intensity = ProDrums.Intensity;
                        break;
                    case "harmVocals":
                    case "vocal_harm":
                        condiffs.HarmonyVocals = (short) diff;
                        SetRank(ref HarmonyVocals.Intensity, diff, HarmonyDiffMap);
                        if (LeadVocals.Intensity == -1)
                            LeadVocals.Intensity = HarmonyVocals.Intensity;
                        break;
                    case "band":
                        condiffs.band = (short) diff;
                        SetRank(ref _bandDifficulty.Intensity, diff, BandDiffMap);
                        _bandDifficulty.SubTracks = 1;
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
