using YARG.Core.IO;
using YARG.Core.IO.Ini;

namespace YARG.Core.Song
{
    /// <summary>
    /// Bridges a parsed <see cref="DTAEntry"/> into an <see cref="IniModifierCollection"/>,
    /// so a DTA-fallback ini song can flow through the exact same
    /// <see cref="IniSubEntry.ScanChart"/> pipeline as a real song.ini.
    ///
    /// All DTA decoding/formatting knowledge lives in the shared
    /// <see cref="SongMetadata.FillFromDTA"/> (the same method RBCONEntry uses) - this only
    /// decides, per field, whether the DTA actually specified it (<see cref="DTAEntry"/>'s
    /// nullable fields) and copies the resulting value across to the matching modifier key.
    /// </summary>
    internal static class DTAMetadataAdapter
    {
        // Same rank->tier thresholds RBCONEntry.GetIntensity uses (see SongEntry.RBCON.cs) -
        // a DTA's "rank" values are RB's internal point-based scale, not a direct 0-6 tier
        // like ini's diff_* keys expect, so they need the same conversion here.
        private static readonly int[] BandDiffMap       = { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap     = { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] DrumDiffMap       = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealKeysDiffMap   = { 153, 211, 269, 327, 385, 443 };

        private static int GetIntensity(int rank, int[] values)
        {
            int intensity = 0;
            while (intensity < 6 && values[intensity] <= rank)
            {
                ++intensity;
            }
            return intensity;
        }

        public static IniModifierCollection BuildModifiers(string nodeName, in DTAEntry dta)
        {
            var metadata = default(SongMetadata);
            SongMetadata.FillFromDTA(ref metadata, in dta);

            var modifiers = new IniModifierCollection();

            // The DTA node's own key is the RB shortname - CON update matching
            // (songs_updates.dta, upgrades, etc.) keys off this, same as song.ini's
            // "shortname" tag.
            modifiers.SetString("shortname", nodeName);

            var ranks = dta.Intensities;
            if (ranks.Band >= 0)           { modifiers.SetInt32("diff_band", GetIntensity(ranks.Band, BandDiffMap)); }
            if (ranks.FiveFretGuitar >= 0) { modifiers.SetInt32("diff_guitar", GetIntensity(ranks.FiveFretGuitar, GuitarDiffMap)); }
            if (ranks.FiveFretBass >= 0)   { modifiers.SetInt32("diff_bass", GetIntensity(ranks.FiveFretBass, GuitarDiffMap)); }
            if (ranks.Keys >= 0)           { modifiers.SetInt32("diff_keys", GetIntensity(ranks.Keys, GuitarDiffMap)); }
            if (ranks.FourLaneDrums >= 0)  { modifiers.SetInt32("diff_drums", GetIntensity(ranks.FourLaneDrums, DrumDiffMap)); }
            if (ranks.ProDrums >= 0)       { modifiers.SetInt32("diff_drums_real", GetIntensity(ranks.ProDrums, DrumDiffMap)); }
            if (ranks.ProGuitar >= 0)      { modifiers.SetInt32("diff_guitar_real", GetIntensity(ranks.ProGuitar, RealGuitarDiffMap)); }
            if (ranks.ProBass >= 0)        { modifiers.SetInt32("diff_bass_real", GetIntensity(ranks.ProBass, RealGuitarDiffMap)); }
            if (ranks.ProKeys >= 0)        { modifiers.SetInt32("diff_keys_real", GetIntensity(ranks.ProKeys, RealKeysDiffMap)); }
            if (ranks.LeadVocals >= 0)     { modifiers.SetInt32("diff_vocals", GetIntensity(ranks.LeadVocals, GuitarDiffMap)); }
            if (ranks.HarmonyVocals >= 0)  { modifiers.SetInt32("diff_vocals_harm", GetIntensity(ranks.HarmonyVocals, DrumDiffMap)); }

            if (dta.Name != null)              { modifiers.SetString("name", metadata.Name); }
            if (dta.Artist != null)            { modifiers.SetString("artist", metadata.Artist); }
            if (dta.CoveredBy != null)         { modifiers.SetString("covered_by", metadata.CoveredBy); }
            if (dta.Album != null)             { modifiers.SetString("album", metadata.Album); }
            if (dta.Charter != null)           { modifiers.SetString("charter", metadata.Charter); }
            if (dta.CharterKeys != null)
            {
                modifiers.SetString("charter_keys", metadata.CharterKeys);
                modifiers.SetString("charter_pro_keys", metadata.CharterProKeys);
            }
            if (dta.CharterProStrings != null)
            {
                modifiers.SetString("charter_pro_guitar", metadata.CharterProGuitar);
                modifiers.SetString("charter_pro_bass", metadata.CharterProBass);
            }
            if (dta.LoadingPhrase != null)     { modifiers.SetString("loading_phrase", metadata.LoadingPhrase); }
            if (dta.Playlist != null)          { modifiers.SetString("playlist", metadata.Playlist); }
            if (dta.Genre != null)             { modifiers.SetString("genre", metadata.Genre); }
            if (dta.Subgenre != null)          { modifiers.SetString("sub_genre", metadata.Subgenre); }
            // "icon" is the ini's name for what the DTA calls "source" (game_origin)
            if (dta.Source != null)            { modifiers.SetString("icon", metadata.Source); }
            if (dta.YearAsNumber != null)      { modifiers.SetString("year", metadata.Year); }
            if (dta.VocalGender != null)       { modifiers.SetString("vocal_gender", metadata.VocalGender.ToString()); }
            if (dta.SongLength != null)        { modifiers.SetInt64("song_length", metadata.SongLength); }
            if (dta.AlbumTrack != null)        { modifiers.SetInt32("album_track", metadata.AlbumTrack); }
            if (dta.Preview != null)           { modifiers.SetInt64Array("preview", metadata.Preview.Start, metadata.Preview.End); }

            return modifiers;
        }
    }
}
