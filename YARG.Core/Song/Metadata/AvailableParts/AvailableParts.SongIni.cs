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
                _bandDifficulty.intensity = (sbyte) intensity;
                if (intensity != -1)
                    _bandDifficulty.subTracks = 1;
            }

            if (modifiers.TryGet("diff_guitar", out intensity))
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
                    FourLaneDrums.intensity = ProDrums.intensity;
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity))
                ProGuitar_22Fret.intensity = ProGuitar_17Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_bass_real", out intensity))
                ProBass_22Fret.intensity = ProBass_17Fret.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_guitar_real_22", out intensity))
            {
                ProGuitar_22Fret.intensity = (sbyte) intensity;
                if (ProGuitar_17Fret.intensity == -1)
                    ProGuitar_17Fret.intensity = ProGuitar_22Fret.intensity;
            }

            if (modifiers.TryGet("diff_bass_real_22", out intensity))
            {
                ProBass_22Fret.intensity = (sbyte) intensity;
                if (ProBass_17Fret.intensity == -1)
                    ProBass_17Fret.intensity = ProBass_22Fret.intensity;
            }

            if (modifiers.TryGet("diff_vocals", out intensity))
                HarmonyVocals.intensity = LeadVocals.intensity = (sbyte) intensity;

            if (modifiers.TryGet("diff_vocals_harm", out intensity))
            {
                HarmonyVocals.intensity = (sbyte) intensity;
                if (LeadVocals.intensity == -1)
                    LeadVocals.intensity = HarmonyVocals.intensity;
            }
        }
    }
}
