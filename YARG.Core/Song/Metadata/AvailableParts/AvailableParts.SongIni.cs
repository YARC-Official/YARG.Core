using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        public void SetIntensities(IniSection modifiers)
        {
            if (modifiers.TryGet("diff_band", out int intensity))
                _bandDifficulty = (sbyte) intensity;

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
