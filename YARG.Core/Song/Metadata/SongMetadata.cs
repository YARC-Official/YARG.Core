using System;
using System.Text.RegularExpressions;
using YARG.Core.Chart;

#nullable enable
namespace YARG.Core.Song
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
        IniEntryCorruption,
        NoName,
        NoNotes,
        DTAError,
        MoggError,
        UnsupportedEncryption,
        MissingMidi,
        MissingUpdateMidi,
        MissingUpgradeMidi,
        PossibleCorruption,

        LooseChart_NoAudio,
        PathTooLong
    }

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
    public sealed partial class SongMetadata
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

        private string _directory = string.Empty;

        private SortString _name = string.Empty;
        private SortString _artist = DEFAULT_ARTIST;
        private SortString _album = DEFAULT_ALBUM;
        private SortString _genre = DEFAULT_GENRE;
        private SortString _charter = DEFAULT_CHARTER;
        private SortString _source = DEFAULT_SOURCE;
        private SortString _playlist = string.Empty;

        private string _unmodifiedYear = DEFAULT_YEAR;
        private string _parsedYear = DEFAULT_YEAR;
        private int _intYear = int.MaxValue;

        private bool _isMaster = true;

        private int _albumTrack = 0;
        private int _playlistTrack = 0;

        private string _loadingPhrase = string.Empty;

        private double _songLength = 0;
        private double _songOffset = 0;

        private double _previewStart = 0;
        private double _previewEnd = 0;

        private double _videoStartTime = 0;
        private double _videoEndTime = -1;

        private HashWrapper _hash = default;

        private AvailableParts _parts = new();

        private ParseSettings _parseSettings = ParseSettings.Default;

        private readonly IniSubmetadata? _iniData = null;
        private readonly IRBCONMetadata? _rbData = null;

        public string Directory => _directory;

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
            private set
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
            private set
            {
                _intYear = value;
                _parsedYear = _unmodifiedYear = value.ToString();
            }
        }

        public bool IsMaster => _isMaster;

        public int AlbumTrack => _albumTrack;
        public int PlaylistTrack => _playlistTrack;

        public string LoadingPhrase => _loadingPhrase;

        public double SongLength => _songLength;
        public double SongOffset => _songOffset;

        public ulong SongLengthMilliseconds => (ulong) (SongLength * MILLISECOND_FACTOR);
        public long SongOffsetMilliseconds => (long) (SongOffset * MILLISECOND_FACTOR);

        public double PreviewStart => _previewStart;
        public double PreviewEnd => _previewEnd;

        public ulong PreviewStartMilliseconds => (ulong) (PreviewStart * MILLISECOND_FACTOR);
        public ulong PreviewEndMilliseconds => (ulong) (PreviewEnd * MILLISECOND_FACTOR);

        public double VideoStartTime => _videoStartTime;
        public double VideoEndTime => _videoEndTime;

        public long VideoStartTimeMilliseconds => (long) (VideoStartTime * MILLISECOND_FACTOR);
        public long VideoEndTimeMilliseconds => (long) (VideoEndTime * MILLISECOND_FACTOR);

        public HashWrapper Hash => _hash;

        public AvailableParts Parts => _parts;

        public ParseSettings ParseSettings => _parseSettings;

        public IniSubmetadata? IniData => _iniData;
        public IRBCONMetadata? RBData => _rbData;

        public SongMetadata() { }

        public SongChart LoadChart()
        {
            if (IniData != null)
            {
                return LoadIniChart();
            }
            else if (RBData != null)
            {
                return LoadCONChart();
            }

            // This is an invalid state, notify about it
            string errorMessage = $"No chart data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public override string ToString() { return _artist + " | " + _name; }
    }
}