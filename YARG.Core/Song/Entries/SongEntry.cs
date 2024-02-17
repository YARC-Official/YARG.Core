using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Chart;
using YARG.Core.Venue;

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

    public enum EntryType
    {
        Ini,
        Sng,
        ExCON,
        CON,
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
    public abstract partial class SongEntry
    {
        public const double MILLISECOND_FACTOR = 1000.0;
        protected static readonly string[] BACKGROUND_FILENAMES =
        {
            "bg", "background", "video"
        };

        protected static readonly string[] VIDEO_EXTENSIONS =
        {
            ".mp4", ".mov", ".webm",
        };

        protected static readonly string[] IMAGE_EXTENSIONS =
        {
            ".png", ".jpg", ".jpeg"
        };

        protected static readonly string YARGROUND_EXTENSION = ".yarground";
        protected static readonly string YARGROUND_FULLNAME = "bg.yarground";

        private static readonly Random YARGROUND_RNG = new();
        protected static BackgroundResult? SelectRandomYarground(string directory)
        {
            var venues = new List<string>();
            foreach (var file in System.IO.Directory.EnumerateFiles(directory))
            {
                if (Path.GetExtension(file) == YARGROUND_EXTENSION)
                {
                    venues.Add(file);
                }
            }

            if (venues.Count == 0)
            {
                return null;
            }
            
            var stream = File.OpenRead(venues[YARGROUND_RNG.Next(venues.Count)]);
            return new BackgroundResult(BackgroundType.Yarground, stream);
        }

        private string _parsedYear;
        private int _intYear;

        protected SongMetadata Metadata;

        public abstract string Directory { get; }

        public abstract EntryType SubType { get; }

        public SortString Name => Metadata.Name;
        public SortString Artist => Metadata.Artist;
        public SortString Album => Metadata.Album;
        public SortString Genre => Metadata.Genre;
        public SortString Charter => Metadata.Charter;
        public SortString Source => Metadata.Source;
        public SortString Playlist => Metadata.Playlist;

        public string Year => _parsedYear;

        public string UnmodifiedYear => Metadata.Year;

        public int YearAsNumber
        {
            get => _intYear;
            set
            {
                _intYear = value;
                _parsedYear = Metadata.Year = value.ToString();
            }
        }

        public bool IsMaster => Metadata.IsMaster;

        public int AlbumTrack => Metadata.AlbumTrack;

        public int PlaylistTrack => Metadata.PlaylistTrack;

        public string LoadingPhrase => Metadata.LoadingPhrase;

        public ulong SongLengthMilliseconds
        {
            get => Metadata.SongLength;
            set => Metadata.SongLength = value;
        }

        public long SongOffsetMilliseconds
        {
            get => Metadata.SongOffset;
            set => Metadata.SongOffset = value;
        }

        public double SongLengthSeconds
        {
            get => Metadata.SongLength / MILLISECOND_FACTOR;
            set => Metadata.SongLength = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            get => Metadata.SongOffset / MILLISECOND_FACTOR;
            set => Metadata.SongOffset = (long) (value * MILLISECOND_FACTOR);
        }

        public ulong PreviewStartMilliseconds
        {
            get => Metadata.PreviewStart;
            set => Metadata.PreviewStart = value;
        }

        public ulong PreviewEndMilliseconds
        {
            get => Metadata.PreviewEnd;
            set => Metadata.PreviewEnd = value;
        }

        public double PreviewStartSeconds
        {
            get => Metadata.PreviewStart / MILLISECOND_FACTOR;
            set => Metadata.PreviewStart = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            get => Metadata.PreviewEnd / MILLISECOND_FACTOR;
            set => Metadata.PreviewEnd = (ulong) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            get => Metadata.VideoStartTime;
            set => Metadata.VideoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            get => Metadata.VideoEndTime;
            set => Metadata.VideoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            get => Metadata.VideoStartTime / MILLISECOND_FACTOR;
            set => Metadata.VideoStartTime = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            get => Metadata.VideoEndTime >= 0 ? Metadata.VideoEndTime / MILLISECOND_FACTOR : -1;
            set => Metadata.VideoEndTime = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public HashWrapper Hash => Metadata.Hash;

        public AvailableParts Parts => Metadata.Parts;

        public ParseSettings ParseSettings => Metadata.ParseSettings;

        public override string ToString() { return Metadata.Artist + " | " + Metadata.Name; }

        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        protected SongEntry()
        {
            Metadata = SongMetadata.Default;
            _parsedYear = SongMetadata.DEFAULT_YEAR;
            _intYear = int.MaxValue;
        }

        protected SongEntry(in SongMetadata metadata)
        {
            Metadata = metadata;
            var match = s_YearRegex.Match(Metadata.Year);
            if (string.IsNullOrEmpty(match.Value))
            {
                _parsedYear = Metadata.Year;
                _intYear = int.MaxValue;
            }
            else
            {
                _parsedYear = match.Value[..4];
                _intYear = int.Parse(_parsedYear);
            }
        }
    }
}