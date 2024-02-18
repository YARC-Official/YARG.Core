using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.IO.Ini;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        public void SetIntensities(IniSection modifiers)
        {
            if (modifiers.TryGet("diff_band", out int intensity))
            {
                _bandDifficulty.Intensity = (sbyte) intensity;
                if (intensity != -1)
                {
                    _bandDifficulty.SubTracks = 1;
                }
            }

            if (modifiers.TryGet("diff_guitar", out intensity))
            {
                ProGuitar_22Fret.Intensity = ProGuitar_17Fret.Intensity = FiveFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bass", out intensity))
            {
                ProBass_22Fret.Intensity = ProBass_17Fret.Intensity = FiveFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm", out intensity))
            {
                FiveFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop", out intensity))
            {
                FiveFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitarghl", out intensity))
            {
                SixFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bassghl", out intensity))
            {
                SixFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm_ghl", out intensity))
            {
                SixFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop_ghl", out intensity))
            {
                SixFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_keys", out intensity))
            {
                ProKeys.Intensity = Keys.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums", out intensity))
            {
                FourLaneDrums.Intensity = (sbyte) intensity;
                ProDrums.Intensity = (sbyte) intensity;
                FiveLaneDrums.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums_real", out intensity))
            {
                ProDrums.Intensity = (sbyte) intensity;
                if (FourLaneDrums.Intensity == -1)
                {
                    FourLaneDrums.Intensity = ProDrums.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity))
            {
                ProGuitar_22Fret.Intensity = ProGuitar_17Fret.Intensity = (sbyte) intensity;
                if (FiveFretGuitar.Intensity == -1)
                {
                    FiveFretGuitar.Intensity = ProGuitar_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real", out intensity))
            {
                ProBass_22Fret.Intensity = ProBass_17Fret.Intensity = (sbyte) intensity;
                if (FiveFretBass.Intensity == -1)
                {
                    FiveFretBass.Intensity = ProBass_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real_22", out intensity))
            {
                ProGuitar_22Fret.Intensity = (sbyte) intensity;
                if (ProGuitar_17Fret.Intensity == -1)
                {
                    ProGuitar_17Fret.Intensity = ProGuitar_22Fret.Intensity;
                }

                if (FiveFretGuitar.Intensity == -1)
                {
                    FiveFretGuitar.Intensity = ProGuitar_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real_22", out intensity))
            {
                ProBass_22Fret.Intensity = (sbyte) intensity;
                if (ProBass_17Fret.Intensity == -1)
                {
                    ProBass_17Fret.Intensity = ProBass_22Fret.Intensity;
                }

                if (FiveFretBass.Intensity == -1)
                {
                    FiveFretBass.Intensity = ProBass_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_keys_real", out intensity))
            {
                ProKeys.Intensity = (sbyte) intensity;
                if (Keys.Intensity == -1)
                {
                    Keys.Intensity = ProKeys.Intensity;
                }
            }

            if (modifiers.TryGet("diff_vocals", out intensity))
            {
                HarmonyVocals.Intensity = LeadVocals.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_vocals_harm", out intensity))
            {
                HarmonyVocals.Intensity = (sbyte) intensity;
                if (LeadVocals.Intensity == -1)
                {
                    LeadVocals.Intensity = HarmonyVocals.Intensity;
                }
            }
        }
    }
}
