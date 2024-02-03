using System;
using System.Text.RegularExpressions;
using YARG.Core.Chart;

namespace YARG.Core.Song
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
        IniEntryCorruption,
        IniNotDownloaded,
        ChartNotDownloaded,
        NoName,
        NoNotes,
        DTAError,
        MoggError,
        UnsupportedEncryption,
        MissingMidi,
        MissingUpdateMidi,
        MissingUpgradeMidi,
        PossibleCorruption,
        FailedSngLoad,

        NoAudio,
        PathTooLong,
        MultipleMidiTrackNames,
        MultipleMidiTrackNames_Update,
        MultipleMidiTrackNames_Upgrade,

        LooseChart_Warning
    }

    /// <summary>
    /// The type of chart file to read.
    /// </summary>
    public enum ChartType
    {
        Mid,
        Midi,
        Chart,
    };

    /// <summary>
    /// The metadata for a song.
    /// </summary>
    /// <remarks>
    /// This class is intended to hold all metadata for all songs, whether it be displayed in the song list or used for
    /// parsing/loading of the song.
    /// <br/>
    /// Display/common metadata should be added directly to this class. Metadata only used in a specific file type
    /// should not be handled through inheritance, make a separate class for that data instead and add it as a field to
    /// this one.
    /// <br/>
    /// Instances of this class should not be created directly (except for things like a chart editor), instead they
    /// should be created through static methods which parse in a metadata file of a specific type and return an
    /// instance.
    /// </remarks>
    [Serializable]
    public partial class SongMetadata
    {
        public const int SIZEOF_DATETIME = 8;
        public const double MILLISECOND_FACTOR = 1000.0;

        public const string DEFAULT_YEAR = "Unknown Year";

        public static readonly SortString DEFAULT_NAME    = "Unknown Name";
        public static readonly SortString DEFAULT_ARTIST  = "Unknown Artist";
        public static readonly SortString DEFAULT_ALBUM   = "Unknown Album";
        public static readonly SortString DEFAULT_GENRE   = "Unknown Genre";
        public static readonly SortString DEFAULT_CHARTER = "Unknown Charter";
        public static readonly SortString DEFAULT_SOURCE  = "Unknown Source";

        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        protected SortString _name = SortString.Empty;
        protected SortString _artist = DEFAULT_ARTIST;
        protected SortString _album = DEFAULT_ALBUM;
        protected SortString _genre = DEFAULT_GENRE;
        protected SortString _charter = DEFAULT_CHARTER;
        protected SortString _source = DEFAULT_SOURCE;
        protected SortString _playlist = SortString.Empty;

        protected string _unmodifiedYear = DEFAULT_YEAR;
        protected string _parsedYear = DEFAULT_YEAR;
        protected int _intYear = int.MaxValue;

        protected bool _isMaster = true;

        protected int _albumTrack = 0;
        protected int _playlistTrack = 0;

        protected string _loadingPhrase = string.Empty;

        protected ulong _songLength = 0;
        protected long _songOffset = 0;

        protected ulong _previewStart = 0;
        protected ulong _previewEnd = 0;

        protected long _videoStartTime = 0;
        protected long _videoEndTime = -1;

        protected HashWrapper _hash = default;

        protected AvailableParts _parts = new();

        protected ParseSettings _parseSettings = ParseSettings.Default;

        public virtual string Directory => string.Empty;

        public SortString Name => _name;
        public SortString Artist => _artist;
        public SortString Album => _album;
        public SortString Genre => _genre;
        public SortString Charter => _charter;
        public SortString Source => _source;
        public SortString Playlist => _playlist;

        public string Year
        {
            get => _parsedYear;
            protected set
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
            protected set
            {
                _intYear = value;
                _parsedYear = _unmodifiedYear = value.ToString();
            }
        }

        public bool IsMaster => _isMaster;

        public int AlbumTrack => _albumTrack;
        public int PlaylistTrack => _playlistTrack;

        public string LoadingPhrase => _loadingPhrase;

        public ulong SongLengthMilliseconds
        {
            get => _songLength;
            protected set => _songLength = value;
        }

        public long SongOffsetMilliseconds
        {
            get => _songOffset;
            protected set => _songOffset = value;
        }

        public double SongLengthSeconds
        {
            get => SongLengthMilliseconds / MILLISECOND_FACTOR;
            protected set => SongLengthMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            get => SongOffsetMilliseconds / MILLISECOND_FACTOR;
            protected set => SongOffsetMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public ulong PreviewStartMilliseconds
        {
            get => _previewStart;
            protected set => _previewStart = value;
        }

        public ulong PreviewEndMilliseconds
        {
            get => _previewEnd;
            protected set => _previewEnd = value;
        }

        public double PreviewStartSeconds
        {
            get => PreviewStartMilliseconds / MILLISECOND_FACTOR;
            protected set => PreviewStartMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            get => PreviewEndMilliseconds / MILLISECOND_FACTOR;
            protected set => PreviewEndMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            get => _videoStartTime;
            protected set => _videoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            get => _videoEndTime;
            protected set => _videoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            get => VideoStartTimeMilliseconds / MILLISECOND_FACTOR;
            protected set => VideoStartTimeMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            get => VideoEndTimeMilliseconds >= 0 ? VideoEndTimeMilliseconds / MILLISECOND_FACTOR : -1;
            protected set => VideoEndTimeMilliseconds = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public HashWrapper Hash => _hash;

        public AvailableParts Parts => _parts;

        public ParseSettings ParseSettings => _parseSettings;

        public SongMetadata() { }

        public override string ToString() { return _artist + " | " + _name; }
    }
}