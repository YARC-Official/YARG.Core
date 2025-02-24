﻿using YARG.Core.IO.Ini;
using YARG.Core.IO.Ultrastar;

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
            CreditWrittenBy = string.Empty,
            CreditPerformedBy = string.Empty,
            CreditCourtesyOf = string.Empty,
            CreditAlbumCover = string.Empty,
            CreditLicense = string.Empty,
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

        public string CreditWrittenBy;
        public string CreditPerformedBy;
        public string CreditCourtesyOf;
        public string CreditAlbumCover;
        public string CreditLicense;

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

            if (modifiers.Extract("credit_written_by", out string creditWrittenBy))
            {
                metadata.CreditWrittenBy = creditWrittenBy;
            }

            if (modifiers.Extract("credit_performed_by", out string creditPerformedBy))
            {
                metadata.CreditPerformedBy = creditPerformedBy;
            }

            if (modifiers.Extract("credit_courtesy_of", out string creditCourtesyOf))
            {
                metadata.CreditCourtesyOf = creditCourtesyOf;
            }

            if (modifiers.Extract("credit_album_cover", out string creditAlbumCover))
            {
                metadata.CreditAlbumCover = creditAlbumCover;
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
        }

        public static void FillFromUltrastar(ref SongMetadata metadata, UltrastarVersion version, UltrastarModifierCollection modifiers)
        {
            if (modifiers.Extract("title", out string title) && title.Length > 0)
            {
                metadata.Name = title;
            }

            if (modifiers.Extract("artist", out string artist) && artist.Length > 0)
            {
                metadata.Artist = artist;
            }

            if (modifiers.Extract("genre", out string genre) && genre.Length > 0)
            {
                metadata.Genre = genre;
            }

            if (modifiers.Extract("year", out string year) && year.Length > 0)
            {
                metadata.Year = year;
            }

            if (modifiers.Extract("creator", out string creator) && creator.Length > 0)
            {
                metadata.Charter = creator;
            }

            metadata.Source = "ultrastar";
            metadata.PlaylistTrack = int.MaxValue;
            metadata.AlbumTrack = int.MaxValue;

            if (modifiers.Extract("videogap", out double videoStartTime))
            {
                // In version 2.0.0 this was changed to seconds, instead of milliseconds
                if (version >= UltrastarVersion.V2_0_0)
                {
                    videoStartTime *= MILLISECOND_FACTOR;
                }

                metadata.Video.Start = (long) videoStartTime;
            }

            if (modifiers.Extract("previewstart", out double previewStart))
            {
                if (version >= UltrastarVersion.V2_0_0)
                {
                    previewStart *= MILLISECOND_FACTOR;
                }

                metadata.Preview.Start = (long) previewStart;
            }

            if (modifiers.Extract("previewend", out double previewEnd))
            {
                if (version >= UltrastarVersion.V2_0_0)
                {
                    previewEnd *= MILLISECOND_FACTOR;
                }

                metadata.Preview.End = (long) previewEnd;
            }
        }
    }
}
