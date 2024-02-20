using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.IO.Ini;

namespace YARG.Core.Song
{
    public partial struct AvailableParts
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
                _proGuitar_22Fret.Intensity = _proGuitar_17Fret.Intensity = _fiveFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bass", out intensity))
            {
                _proBass_22Fret.Intensity = _proBass_17Fret.Intensity = _fiveFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm", out intensity))
            {
                _fiveFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop", out intensity))
            {
                _fiveFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitarghl", out intensity))
            {
                _sixFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bassghl", out intensity))
            {
                _sixFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm_ghl", out intensity))
            {
                _sixFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop_ghl", out intensity))
            {
                _sixFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_keys", out intensity))
            {
                _proKeys.Intensity = _keys.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums", out intensity))
            {
                _fourLaneDrums.Intensity = (sbyte) intensity;
                _proDrums.Intensity = (sbyte) intensity;
                _fiveLaneDrums.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums_real", out intensity))
            {
                _proDrums.Intensity = (sbyte) intensity;
                if (_fourLaneDrums.Intensity == -1)
                {
                    _fourLaneDrums.Intensity = _proDrums.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity))
            {
                _proGuitar_22Fret.Intensity = _proGuitar_17Fret.Intensity = (sbyte) intensity;
                if (_fiveFretGuitar.Intensity == -1)
                {
                    _fiveFretGuitar.Intensity = _proGuitar_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real", out intensity))
            {
                _proBass_22Fret.Intensity = _proBass_17Fret.Intensity = (sbyte) intensity;
                if (_fiveFretBass.Intensity == -1)
                {
                    _fiveFretBass.Intensity = _proBass_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real_22", out intensity))
            {
                _proGuitar_22Fret.Intensity = (sbyte) intensity;
                if (_proGuitar_17Fret.Intensity == -1)
                {
                    _proGuitar_17Fret.Intensity = _proGuitar_22Fret.Intensity;
                }

                if (_fiveFretGuitar.Intensity == -1)
                {
                    _fiveFretGuitar.Intensity = _proGuitar_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real_22", out intensity))
            {
                _proBass_22Fret.Intensity = (sbyte) intensity;
                if (_proBass_17Fret.Intensity == -1)
                {
                    _proBass_17Fret.Intensity = _proBass_22Fret.Intensity;
                }

                if (_fiveFretBass.Intensity == -1)
                {
                    _fiveFretBass.Intensity = _proBass_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_keys_real", out intensity))
            {
                _proKeys.Intensity = (sbyte) intensity;
                if (_keys.Intensity == -1)
                {
                    _keys.Intensity = _proKeys.Intensity;
                }
            }

            if (modifiers.TryGet("diff_vocals", out intensity))
            {
                _harmonyVocals.Intensity = _leadVocals.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_vocals_harm", out intensity))
            {
                _harmonyVocals.Intensity = (sbyte) intensity;
                if (_leadVocals.Intensity == -1)
                {
                    _leadVocals.Intensity = _harmonyVocals.Intensity;
                }
            }
        }
    }
}
