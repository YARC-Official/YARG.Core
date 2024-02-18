using YARG.Core.IO;

namespace YARG.Core.Song
{
    public partial struct AvailableParts
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

        public void SetIntensities(ref RBCONDifficulties rbDiffs, YARGDTAReader reader)
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
                        rbDiffs.FourLaneDrums = (short) diff;
                        SetRank(ref _fourLaneDrums.Intensity, diff, DrumDiffMap);
                        if (_proDrums.Intensity == -1)
                        {
                            _proDrums.Intensity = _fourLaneDrums.Intensity;
                        }
                        break;
                    case "guitar":
                        rbDiffs.FiveFretGuitar = (short) diff;
                        SetRank(ref _fiveFretGuitar.Intensity, diff, GuitarDiffMap);
                        if (_proGuitar_17Fret.Intensity == -1)
                        {
                            _proGuitar_22Fret.Intensity = _proGuitar_17Fret.Intensity = _fiveFretGuitar.Intensity;
                        }
                        break;
                    case "bass":
                        rbDiffs.FiveFretBass = (short) diff;
                        SetRank(ref _fiveFretBass.Intensity, diff, BassDiffMap);
                        if (_proBass_17Fret.Intensity == -1)
                        {
                            _proBass_22Fret.Intensity = _proBass_17Fret.Intensity = _fiveFretBass.Intensity;
                        }
                        break;
                    case "vocals":
                        rbDiffs.LeadVocals = (short) diff;
                        SetRank(ref _leadVocals.Intensity, diff, VocalsDiffMap);
                        if (_harmonyVocals.Intensity == -1)
                        {
                            _harmonyVocals.Intensity = _leadVocals.Intensity;
                        }
                        break;
                    case "keys":
                        rbDiffs.Keys = (short) diff;
                        SetRank(ref _keys.Intensity, diff, KeysDiffMap);
                        if (_proKeys.Intensity == -1)
                        {
                            _proKeys.Intensity = _keys.Intensity;
                        }
                        break;
                    case "realGuitar":
                    case "real_guitar":
                        rbDiffs.ProGuitar = (short) diff;
                        SetRank(ref _proGuitar_17Fret.Intensity, diff, RealGuitarDiffMap);
                        _proGuitar_22Fret.Intensity = _proGuitar_17Fret.Intensity;
                        if (_fiveFretGuitar.Intensity == -1)
                        {
                            _fiveFretGuitar.Intensity = _proGuitar_17Fret.Intensity;
                        }
                        break;
                    case "realBass":
                    case "real_bass":
                        rbDiffs.ProBass = (short) diff;
                        SetRank(ref _proBass_17Fret.Intensity, diff, RealBassDiffMap);
                        _proBass_22Fret.Intensity = _proBass_17Fret.Intensity;
                        if (_fiveFretBass.Intensity == -1)
                        {
                            _fiveFretBass.Intensity = _proBass_17Fret.Intensity;
                        }
                        break;
                    case "realKeys":
                    case "real_keys":
                        rbDiffs.ProKeys = (short) diff;
                        SetRank(ref _proKeys.Intensity, diff, RealKeysDiffMap);
                        if (_keys.Intensity == -1)
                        {
                            _keys.Intensity = _proKeys.Intensity;
                        }
                        break;
                    case "realDrums":
                    case "real_drums":
                        rbDiffs.ProDrums = (short) diff;
                        SetRank(ref _proDrums.Intensity, diff, RealDrumsDiffMap);
                        if (_fourLaneDrums.Intensity == -1)
                        {
                            _fourLaneDrums.Intensity = _proDrums.Intensity;
                        }
                        break;
                    case "harmVocals":
                    case "vocal_harm":
                        rbDiffs.HarmonyVocals = (short) diff;
                        SetRank(ref _harmonyVocals.Intensity, diff, HarmonyDiffMap);
                        if (_leadVocals.Intensity == -1)
                        {
                            _leadVocals.Intensity = _harmonyVocals.Intensity;
                        }
                        break;
                    case "band":
                        rbDiffs.Band = (short) diff;
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
