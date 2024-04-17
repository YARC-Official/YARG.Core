using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public struct SongMetadata
    {
        public static readonly SortString DEFAULT_NAME = "Unknown Name";
        public static readonly SortString DEFAULT_ARTIST = "Unknown Artist";
        public static readonly SortString DEFAULT_ALBUM = "Unknown Album";
        public static readonly SortString DEFAULT_GENRE = "Unknown Genre";
        public static readonly SortString DEFAULT_CHARTER = "Unknown Charter";
        public static readonly SortString DEFAULT_SOURCE = "Unknown Source";
        public const string DEFAULT_YEAR = "Unknown Year";

        public static readonly SongMetadata Default = new()
        {
            Name = SortString.Empty,
            Artist = DEFAULT_ARTIST,
            Album = DEFAULT_ALBUM,
            Genre = DEFAULT_GENRE,
            Charter = DEFAULT_CHARTER,
            Source = DEFAULT_SOURCE,
            Playlist = SortString.Empty,
            IsMaster = true,
            AlbumTrack = 0,
            PlaylistTrack = 0,
            LoadingPhrase = string.Empty,
            Year = DEFAULT_YEAR,
            SongLength = 0,
            SongOffset = 0,
            PreviewStart = -1,
            PreviewEnd = -1,
            VideoStartTime = 0,
            VideoEndTime = -1,
        };

        public SortString Name;
        public SortString Artist;
        public SortString Album;
        public SortString Genre;
        public SortString Charter;
        public SortString Source;
        public SortString Playlist;

        public string Year;

        public ulong SongLength;
        public long SongOffset;
        public uint SongRating;  // 1 = FF; 2 = SR; 3 = M; 4 = NR

        public long PreviewStart;
        public long PreviewEnd;

        public long VideoStartTime;
        public long VideoEndTime;

        public bool IsMaster;

        public int AlbumTrack;
        public int PlaylistTrack;

        public string LoadingPhrase;
    }
}
