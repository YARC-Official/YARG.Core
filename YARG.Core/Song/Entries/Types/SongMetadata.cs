using System.Text.RegularExpressions;
using YARG.Core.Chart;

namespace YARG.Core.Song
{
    public partial struct SongMetadata
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
            Hash = default,
            Parts = new(),
            ParseSettings = ParseSettings.Default,
            LoadingPhrase = string.Empty,
            _unmodifiedYear = DEFAULT_YEAR,
            _parsedYear = DEFAULT_YEAR,
            _intYear = int.MaxValue,
            _songLength = 0,
            _songOffset = 0,
            _previewStart = 0,
            _previewEnd = 0,
            _videoStartTime = 0,
            _videoEndTime = -1,
        };

        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        public const int SIZEOF_DATETIME = 8;
        public const double MILLISECOND_FACTOR = 1000.0;

        public SortString Name;
        public SortString Artist;
        public SortString Album;
        public SortString Genre;
        public SortString Charter;
        public SortString Source;
        public SortString Playlist;

        public bool IsMaster;

        public int AlbumTrack;
        public int PlaylistTrack;

        public HashWrapper Hash;

        public AvailableParts Parts;

        public ParseSettings ParseSettings;

        public string LoadingPhrase;

        private string _unmodifiedYear;
        private string _parsedYear;
        private int _intYear;

        private ulong _songLength;
        private long _songOffset;

        private ulong _previewStart;
        private ulong _previewEnd;

        private long _videoStartTime;
        private long _videoEndTime;

        public string Year
        {
            get => _parsedYear;
            set
            {
                _unmodifiedYear = value;
                var match = s_YearRegex.Match(value);
                if (string.IsNullOrEmpty(match.Value))
                    _parsedYear = value;
                else
                {
                    _parsedYear = match.Value[..4];
                    _intYear = int.Parse(_parsedYear);
                }
            }
        }

        public string UnmodifiedYear => _unmodifiedYear;

        public int YearAsNumber
        {
            get => _intYear;
            set
            {
                _intYear = value;
                _parsedYear = _unmodifiedYear = value.ToString();
            }
        }

        public ulong SongLengthMilliseconds
        {
            readonly get => _songLength;
            set => _songLength = value;
        }

        public long SongOffsetMilliseconds
        {
            readonly get => _songOffset;
            set => _songOffset = value;
        }

        public double SongLengthSeconds
        {
            readonly get => SongLengthMilliseconds / MILLISECOND_FACTOR;
            set => SongLengthMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            readonly get => SongOffsetMilliseconds / MILLISECOND_FACTOR;
            set => SongOffsetMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public ulong PreviewStartMilliseconds
        {
            readonly get => _previewStart;
            set => _previewStart = value;
        }

        public ulong PreviewEndMilliseconds
        {
            readonly get => _previewEnd;
            set => _previewEnd = value;
        }

        public double PreviewStartSeconds
        {
            readonly get => PreviewStartMilliseconds / MILLISECOND_FACTOR;
            set => PreviewStartMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            readonly get => PreviewEndMilliseconds / MILLISECOND_FACTOR;
            set => PreviewEndMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            readonly get => _videoStartTime;
            set => _videoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            readonly get => _videoEndTime;
            set => _videoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            readonly get => VideoStartTimeMilliseconds / MILLISECOND_FACTOR;
            set => VideoStartTimeMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            readonly get => VideoEndTimeMilliseconds >= 0 ? VideoEndTimeMilliseconds / MILLISECOND_FACTOR : -1;
            set => VideoEndTimeMilliseconds = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public override string ToString() { return Artist + " | " + Name; }
    }
}
