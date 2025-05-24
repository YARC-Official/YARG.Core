using YARG.Core.IO.Ini;

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
        public const string DEFAULT_STARS = "Not Played";

        public static readonly SongMetadata Default = new()
        {
            Name = DEFAULT_NAME,
            Artist = DEFAULT_ARTIST,
            Album = DEFAULT_ALBUM,
            Genre = DEFAULT_GENRE,
            Charter = DEFAULT_CHARTER,
            Source = DEFAULT_SOURCE,
            Year = DEFAULT_YEAR,
            Stars = DEFAULT_STARS,
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
        public string Stars;

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

        public static SongMetadata CreateFromIni(IniModifierCollection modifiers)
        {
            var metadata = Default;
            FillFromIni(ref metadata, modifiers);
            return metadata;
        }

        public static void FillFromIni(ref SongMetadata metadata, IniModifierCollection modifiers)
        {
            if (modifiers.Extract("name", out string name) && name.Length > 0)
            {
                metadata.Name = name;
            }

            if (modifiers.Extract("artist", out string artist) && artist.Length > 0)
            {
                metadata.Artist = artist;
            }

            if (modifiers.Extract("album", out string album) && album.Length > 0)
            {
                metadata.Album = album;
            }

            if (modifiers.Extract("genre", out string genre) && genre.Length > 0)
            {
                metadata.Genre = genre;
            }

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

            if (modifiers.Extract("icon", out string source) && source.Length > 0)
            {
                metadata.Source = source;
            }

            if (modifiers.Extract("playlist", out string playlist) && playlist.Length > 0)
            {
                metadata.Playlist = playlist;
            }

            if (modifiers.Extract("loading_phrase", out string loadingPhrase))
            {
                metadata.LoadingPhrase = loadingPhrase;
            }

            if (modifiers.Extract("link_bluesky", out string linkBluesky))
            {
                metadata.LinkBluesky = linkBluesky;
            }

            if (modifiers.Extract("link_facebook", out string linkFacebook))
            {
                metadata.LinkFacebook = linkFacebook;
            }

            if (modifiers.Extract("link_instagram", out string linkInstagram))
            {
                metadata.LinkInstagram = linkInstagram;
            }

            if (modifiers.Extract("link_spotify", out string linkSpotify))
            {
                metadata.LinkSpotify = linkSpotify;
            }

            if (modifiers.Extract("link_twitter", out string linkTwitter))
            {
                metadata.LinkTwitter = linkTwitter;
            }

            if (modifiers.Extract("link_other", out string linkOther))
            {
                metadata.LinkOther = linkOther;
            }

            if (modifiers.Extract("link_youtube", out string linkYoutube))
            {
                metadata.LinkYoutube = linkYoutube;
            }

            if (modifiers.Extract("location", out string location))
            {
                metadata.Location = location;
            }

            if (modifiers.Extract("credit_album_art_designed_by", out string creditAlbumArt))
            {
                metadata.CreditAlbumArtDesignedBy = creditAlbumArt;
            }

            if (modifiers.Extract("credit_arranged_by", out string creditArrangedBy))
            {
                metadata.CreditArrangedBy = creditArrangedBy;
            }

            if (modifiers.Extract("credit_composed_by", out string creditComposedBy))
            {
                metadata.CreditComposedBy = creditComposedBy;
            }

            if (modifiers.Extract("credit_courtesy_of", out string creditCourtesyOf))
            {
                metadata.CreditCourtesyOf = creditCourtesyOf;
            }

            if (modifiers.Extract("credit_engineered_by", out string creditEngineeredBy))
            {
                metadata.CreditEngineeredBy = creditEngineeredBy;
            }

            if (modifiers.Extract("credit_license", out string creditLicense))
            {
                metadata.CreditLicense = creditLicense;
            }

            if (modifiers.Extract("credit_mastered_by", out string creditMasteredBy))
            {
                metadata.CreditMasteredBy = creditMasteredBy;
            }

            if (modifiers.Extract("credit_mixed_by", out string creditMixedBy))
            {
                metadata.CreditMixedBy = creditMixedBy;
            }

            if (modifiers.Extract("credit_other", out string creditOther))
            {
                metadata.CreditOther = creditOther;
            }

            if (modifiers.Extract("credit_performed_by", out string creditPerformedBy))
            {
                metadata.CreditPerformedBy = creditPerformedBy;
            }

            if (modifiers.Extract("credit_produced_by", out string creditProducedBy))
            {
                metadata.CreditProducedBy = creditProducedBy;
            }

            if (modifiers.Extract("credit_published_by", out string creditPublishedBy))
            {
                metadata.CreditPublishedBy = creditPublishedBy;
            }

            if (modifiers.Extract("credit_written_by", out string creditWrittenBy))
            {
                metadata.CreditWrittenBy = creditWrittenBy;
            }

            if (modifiers.Extract("charter_bass", out string charterBass))
            {
                metadata.CharterBass = charterBass;
            }

            if (modifiers.Extract("charter_drums", out string charterDrums))
            {
                metadata.CharterDrums = charterDrums;
            }

            if (modifiers.Extract("charter_elite_drums", out string charterEliteDrums))
            {
                metadata.CharterEliteDrums = charterEliteDrums;
            }

            if (modifiers.Extract("charter_guitar", out string charterGuitar))
            {
                metadata.CharterGuitar = charterGuitar;
            }

            if (modifiers.Extract("charter_keys", out string charterKeys))
            {
                metadata.CharterKeys = charterKeys;
            }

            if (modifiers.Extract("charter_lower_diff", out string charterLowerDiff))
            {
                metadata.CharterLowerDiff = charterLowerDiff;
            }

            if (modifiers.Extract("charter_pro_bass", out string charterProBass))
            {
                metadata.CharterProBass = charterProBass;
            }

            if (modifiers.Extract("charter_pro_keys", out string charterProKeys))
            {
                metadata.CharterProKeys = charterProKeys;
            }

            if (modifiers.Extract("charter_pro_guitar", out string charterProGuitar))
            {
                metadata.CharterProGuitar = charterProGuitar;
            }

            if (modifiers.Extract("charter_vocals", out string charterVocals))
            {
                metadata.CharterVocals = charterVocals;
            }

            if (modifiers.Extract("playlist_track", out int playlistTrack))
            {
                metadata.PlaylistTrack = playlistTrack;
            }
            else
            {
                metadata.PlaylistTrack = int.MaxValue;
            }

            if (modifiers.Extract("album_track", out int albumTrack))
            {
                metadata.AlbumTrack = albumTrack;
            }
            else
            {
                metadata.AlbumTrack = int.MaxValue;
            }

            if (modifiers.Extract("rating", out uint songRating))
            {
                metadata.SongRating = (SongRating)songRating;
            }

            if (modifiers.Extract("song_length", out long songLength))
            {
                metadata.SongLength = songLength;
            }

            if (modifiers.Extract("video_start_time", out long videoStartTime))
            {
                metadata.Video.Start = videoStartTime;
            }

            if (modifiers.Extract("video_end_time", out long videoEndTime))
            {
                metadata.Video.End = videoEndTime;
            }

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

            if (modifiers.Extract("link_bandcamp", out string linkBandcamp))
            {
                metadata.LinkBandcamp = linkBandcamp;
            }
        }
    }
}
