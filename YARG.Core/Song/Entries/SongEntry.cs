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
        protected SongMetadata Metadata = SongMetadata.Default;

        public abstract string Directory { get; }

        public abstract EntryType SubType { get; }

        public SortString Name => Metadata.Name;
        public SortString Artist => Metadata.Artist;
        public SortString Album => Metadata.Album;
        public SortString Genre => Metadata.Genre;
        public SortString Charter => Metadata.Charter;
        public SortString Source => Metadata.Source;
        public SortString Playlist => Metadata.Playlist;

        public string Year
        {
            get => Metadata.Year;
            protected set => Metadata.Year = value;
        }

        public string UnmodifiedYear => Metadata.UnmodifiedYear;

        public int YearAsNumber
        {
            get => Metadata.YearAsNumber;
            protected set => Metadata.YearAsNumber = value;
        }

        public bool IsMaster => Metadata.IsMaster;

        public int AlbumTrack => Metadata.AlbumTrack;
        public int PlaylistTrack => Metadata.PlaylistTrack;

        public string LoadingPhrase => Metadata.LoadingPhrase;

        public ulong SongLengthMilliseconds
        {
            get => Metadata.SongLengthMilliseconds;
            protected set => Metadata.SongLengthMilliseconds = value;
        }

        public long SongOffsetMilliseconds
        {
            get => Metadata.SongOffsetMilliseconds;
            protected set => Metadata.SongOffsetMilliseconds = value;
        }

        public double SongLengthSeconds
        {
            get => Metadata.SongLengthSeconds;
            protected set => Metadata.SongLengthSeconds = value;
        }

        public double SongOffsetSeconds
        {
            get => Metadata.SongOffsetSeconds;
            protected set => Metadata.SongOffsetSeconds = value;
        }

        public ulong PreviewStartMilliseconds
        {
            get => Metadata.PreviewStartMilliseconds;
            protected set => Metadata.PreviewStartMilliseconds = value;
        }

        public ulong PreviewEndMilliseconds
        {
            get => Metadata.PreviewEndMilliseconds;
            protected set => Metadata.PreviewEndMilliseconds = value;
        }

        public double PreviewStartSeconds
        {
            get => Metadata.PreviewStartSeconds;
            protected set => Metadata.PreviewStartSeconds = value;
        }

        public double PreviewEndSeconds
        {
            get => Metadata.PreviewEndSeconds;
            protected set => Metadata.PreviewEndSeconds = value;
        }

        public long VideoStartTimeMilliseconds
        {
            get => Metadata.VideoStartTimeMilliseconds;
            protected set => Metadata.VideoStartTimeMilliseconds = value;
        }

        public long VideoEndTimeMilliseconds
        {
            get => Metadata.VideoEndTimeMilliseconds;
            protected set => Metadata.VideoEndTimeMilliseconds = value;
        }

        public double VideoStartTimeSeconds
        {
            get => Metadata.VideoStartTimeSeconds;
            protected set => Metadata.VideoStartTimeSeconds = value;
        }

        public double VideoEndTimeSeconds
        {
            get => Metadata.VideoEndTimeSeconds;
            protected set => Metadata.VideoEndTimeSeconds = value;
        }

        public HashWrapper Hash => Metadata.Hash;

        public AvailableParts Parts => Metadata.Parts;

        public ParseSettings ParseSettings => Metadata.ParseSettings;

        public override string ToString() { return Metadata.ToString(); }
    }
}