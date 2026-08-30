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
        public static IniModifierCollection BuildModifiers(string nodeName, in DTAEntry dta)
        {
            var metadata = default(SongMetadata);
            SongMetadata.FillFromDTA(ref metadata, in dta);

            var modifiers = new IniModifierCollection();

            // The DTA node's own key is the RB shortname - CON update matching
            // (songs_updates.dta, upgrades, etc.) keys off this, same as song.ini's
            // "shortname" tag.
            modifiers.SetString("shortname", nodeName);

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
