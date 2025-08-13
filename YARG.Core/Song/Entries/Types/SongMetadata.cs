using YARG.Core.IO.Ini;
using System.Runtime.CompilerServices;

namespace YARG.Core.Song
{
    public enum SongRating : uint
    {
        Unspecified,
        Family_Friendly,
        Supervision_Recommended,
        Mature,
        No_Rating
    };

    public struct SongMetadata
    {
        public const double MILLISECOND_FACTOR = 1000.0;
        public const string DEFAULT_NAME = "Unknown Name";
        public const string DEFAULT_ARTIST = "Unknown Artist";
        public const string DEFAULT_ALBUM = "Unknown Album";
        public const string DEFAULT_GENRE = "Unknown Genre";
        public const string DEFAULT_CHARTER = "Unknown Charter";
        public const string DEFAULT_SOURCE = "Unknown Source";
        public const string DEFAULT_YEAR = "####";

        public static readonly SongMetadata Default = new()
        {
            Name = DEFAULT_NAME,
            Artist = DEFAULT_ARTIST,
            Album = DEFAULT_ALBUM,
            Genre = DEFAULT_GENRE,
            Charter = DEFAULT_CHARTER,
            Source = DEFAULT_SOURCE,
            Year = DEFAULT_YEAR,
            Playlist = string.Empty,
            IsMaster = true,
            VideoLoop = false,
            AlbumTrack = int.MaxValue,
            PlaylistTrack = int.MaxValue,
            LoadingPhrase = string.Empty,
            LinkBandcamp = string.Empty,
            LinkBluesky = string.Empty,
            LinkFacebook = string.Empty,
            LinkInstagram = string.Empty,
            LinkSpotify = string.Empty,
            LinkTwitter = string.Empty,
            LinkOther = string.Empty,
            LinkYoutube = string.Empty,
            Location = string.Empty,
            CreditAlbumArtDesignedBy = string.Empty,
            CreditArrangedBy = string.Empty,
            CreditComposedBy = string.Empty,
            CreditCourtesyOf = string.Empty,
            CreditEngineeredBy = string.Empty,
            CreditLicense = string.Empty,
            CreditMasteredBy = string.Empty,
            CreditMixedBy = string.Empty,
            CreditOther = string.Empty,
            CreditPerformedBy = string.Empty,
            CreditProducedBy = string.Empty,
            CreditPublishedBy = string.Empty,
            CreditWrittenBy = string.Empty,
            CharterBass = string.Empty,
            CharterDrums = string.Empty,
            CharterEliteDrums = string.Empty,
            CharterGuitar = string.Empty,
            CharterKeys = string.Empty,
            CharterLowerDiff = string.Empty,
            CharterProBass = string.Empty,
            CharterProKeys = string.Empty,
            CharterProGuitar = string.Empty,
            CharterVocals = string.Empty,
            SongLength = 0,
            SongOffset = 0,
            Preview = (-1, -1),
            Video = (0, -1),
        };

        public string Name;
        public string Artist;
        public string Album;
        public string Genre;
        public string Charter;
        public string Source;
        public string Playlist;
        public string Year;

        public long SongLength;
        public long SongOffset;
        public SongRating SongRating;

        public (long Start, long End) Preview;
        public (long Start, long End) Video;

        public bool IsMaster;
        public bool VideoLoop;

        public int AlbumTrack;
        public int PlaylistTrack;

        public string LoadingPhrase;

        public string LinkBandcamp;
        public string LinkBluesky;
        public string LinkFacebook;
        public string LinkInstagram;
        public string LinkSpotify;
        public string LinkTwitter;
        public string LinkOther;
        public string LinkYoutube;

        public string Location;

        public string CreditAlbumArtDesignedBy;
        public string CreditArrangedBy;
        public string CreditComposedBy;
        public string CreditCourtesyOf;
        public string CreditEngineeredBy;
        public string CreditLicense;
        public string CreditMasteredBy;
        public string CreditMixedBy;
        public string CreditOther;
        public string CreditPerformedBy;
        public string CreditProducedBy;
        public string CreditPublishedBy;
        public string CreditWrittenBy;

        public string CharterBass;
        public string CharterDrums;
        public string CharterEliteDrums;
        public string CharterGuitar;
        public string CharterKeys;
        public string CharterLowerDiff;
        public string CharterProBass;
        public string CharterProKeys;
        public string CharterProGuitar;
        public string CharterVocals;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadIniStringIfNotEmpty(IniModifierCollection modifiers, string modifierName, ref string output) {
            if (modifiers.Extract(modifierName, out string readValue) && readValue.Length > 0)
            {
                output = readValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadIniString(IniModifierCollection modifiers, string modifierName, ref string output) {
            if (modifiers.Extract(modifierName, out string readValue))
            {
                output = readValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadIniIntOr(IniModifierCollection modifiers, string modifierName, out int output, int fallbackValue) {
            if (modifiers.Extract(modifierName, out int readValue))
            {
                output = readValue;
            }
            else
            {
                output = fallbackValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadIniLong(IniModifierCollection modifiers, string modifierName, ref long output) {
            if (modifiers.Extract(modifierName, out long readValue))
            {
                output = readValue;
            }
        }

        public static SongMetadata CreateFromIni(IniModifierCollection modifiers)
        {
            var metadata = Default;
            FillFromIni(ref metadata, modifiers);
            return metadata;
        }

        public static void FillFromIni(ref SongMetadata metadata, IniModifierCollection modifiers)
        {
            ReadIniStringIfNotEmpty(modifiers, "name", ref metadata.Name);
            ReadIniStringIfNotEmpty(modifiers, "artist", ref metadata.Artist);
            ReadIniStringIfNotEmpty(modifiers, "album", ref metadata.Album);
            ReadIniStringIfNotEmpty(modifiers, "genre", ref metadata.Genre);

            if (modifiers.Extract("year", out string year) && year.Length > 0)
            {
                metadata.Year = year;
            }
            else if (modifiers.Extract("year_chart", out year) && year.Length > 0)
            {
                if (year.StartsWith(", "))
                {
                    metadata.Year = year[2..];
                }
                else if (year.StartsWith(','))
                {
                    metadata.Year = year[1..];
                }
                else
                {
                    metadata.Year = year;
                }
            }

            if (modifiers.Extract("charter", out string charter) || modifiers.Extract("frets", out charter))
            {
                if (charter.Length > 0)
                {
                    metadata.Charter = charter;
                }
            }

            ReadIniStringIfNotEmpty(modifiers, "icon", ref metadata.Source);
            ReadIniStringIfNotEmpty(modifiers, "playlist", ref metadata.Playlist);
            ReadIniString(modifiers, "loading_phrase", ref metadata.LoadingPhrase);

            ReadIniString(modifiers, "link_bandcamp", ref metadata.LinkBandcamp);
            ReadIniString(modifiers, "link_bluesky", ref metadata.LinkBluesky);
            ReadIniString(modifiers, "link_facebook", ref metadata.LinkFacebook);
            ReadIniString(modifiers, "link_instagram", ref metadata.LinkInstagram);
            ReadIniString(modifiers, "link_spotify", ref metadata.LinkSpotify);
            ReadIniString(modifiers, "link_twitter", ref metadata.LinkTwitter);
            ReadIniString(modifiers, "link_bluesky", ref metadata.LinkBluesky);
            ReadIniString(modifiers, "link_other", ref metadata.LinkOther);
            ReadIniString(modifiers, "link_youtube", ref metadata.LinkYoutube);

            ReadIniString(modifiers, "location", ref metadata.Location);

            ReadIniString(modifiers, "credit_album_art_designed_by", ref metadata.CreditAlbumArtDesignedBy);
            ReadIniString(modifiers, "credit_arranged_by", ref metadata.CreditArrangedBy);
            ReadIniString(modifiers, "credit_composed_by", ref metadata.CreditComposedBy);
            ReadIniString(modifiers, "credit_courtesy_of", ref metadata.CreditCourtesyOf);
            ReadIniString(modifiers, "credit_engineered_by", ref metadata.CreditEngineeredBy);
            ReadIniString(modifiers, "credit_license", ref metadata.CreditLicense);
            ReadIniString(modifiers, "credit_mastered_by", ref metadata.CreditMasteredBy);
            ReadIniString(modifiers, "credit_mixed_by", ref metadata.CreditMixedBy);
            ReadIniString(modifiers, "credit_other", ref metadata.CreditOther);
            ReadIniString(modifiers, "credit_performed_by", ref metadata.CreditPerformedBy);
            ReadIniString(modifiers, "credit_produced_by", ref metadata.CreditProducedBy);
            ReadIniString(modifiers, "credit_published_by", ref metadata.CreditPublishedBy);
            ReadIniString(modifiers, "credit_written_by", ref metadata.CreditWrittenBy);

            ReadIniString(modifiers, "charter_bass", ref metadata.CharterBass);
            ReadIniString(modifiers, "charter_drums", ref metadata.CharterDrums);
            ReadIniString(modifiers, "charter_elite_drums", ref metadata.CharterEliteDrums);
            ReadIniString(modifiers, "charter_guitar", ref metadata.CharterGuitar);
            ReadIniString(modifiers, "charter_keys", ref metadata.CharterKeys);
            ReadIniString(modifiers, "charter_lower_diff", ref metadata.CharterLowerDiff);
            ReadIniString(modifiers, "charter_pro_bass", ref metadata.CharterProBass);
            ReadIniString(modifiers, "charter_pro_keys", ref metadata.CharterProKeys);
            ReadIniString(modifiers, "charter_pro_guitar", ref metadata.CharterProGuitar);
            ReadIniString(modifiers, "charter_vocals", ref metadata.CharterVocals);

            ReadIniIntOr(modifiers, "playlist_track", out metadata.PlaylistTrack, int.MaxValue);
            ReadIniIntOr(modifiers, "album_track", out metadata.AlbumTrack, int.MaxValue);

            if (modifiers.Extract("rating", out uint songRating))
            {
                metadata.SongRating = (SongRating)songRating;
            }

            ReadIniLong(modifiers, "song_length", ref metadata.SongLength);
            ReadIniLong(modifiers, "video_start_time", ref metadata.Video.Start);
            ReadIniLong(modifiers, "video_end_time", ref metadata.Video.End);

            if (modifiers.Extract("preview", out (long Start, long End) preview))
            {
                metadata.Preview = preview;
            }
            else
            {
                if (modifiers.Extract("preview_start_time", out preview.Start))
                {
                    metadata.Preview.Start = preview.Start;
                }
                else if (modifiers.Extract("preview_start_seconds", out double previewStartSeconds))
                {
                    metadata.Preview.Start = (long)((previewStartSeconds) * MILLISECOND_FACTOR);
                }

                if (modifiers.Extract("preview_end_time", out preview.End))
                {
                    metadata.Preview.End = preview.Item2;
                }
                else if (modifiers.Extract("preview_end_seconds", out double previewEndSeconds))
                {
                    metadata.Preview.End = (long) ((previewEndSeconds) * MILLISECOND_FACTOR);
                }
            }

            if (modifiers.Extract("delay", out long songOffset) && songOffset != 0)
            {
                metadata.SongOffset = songOffset;
            }
            else if (modifiers.Extract("delay_seconds", out double songOffsetSeconds))
            {
                metadata.SongOffset = (long)((songOffsetSeconds) * MILLISECOND_FACTOR);
            }

            if (modifiers.Extract("tags", out string tags))
            {
                metadata.IsMaster = tags.ToLower() != "cover";
            }

            if (modifiers.Extract("video_loop", out bool videoLoop))
            {
                metadata.VideoLoop = videoLoop;
            }
        }
    }
}
