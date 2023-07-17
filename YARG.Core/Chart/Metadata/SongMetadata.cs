using System.Collections.Generic;

namespace YARG.Core.Chart
{
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
    public sealed partial class SongMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;

        public bool IsMaster { get; set; } = true;

        public string Charter { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        public string LoadingPhrase { get; set; } = string.Empty;

        public int AlbumTrack { get; set; } = 0;
        public int PlaylistTrack { get; set; } = 0;

        public double SongLength { get; set; } = 0;

        public double ChartOffset { get; set; } = 0;

        public double PreviewStart { get; set; } = -1;
        public double PreviewEnd { get; set; } = -1;

        public double VideoStartTime { get; set; } = 0;
        public double VideoEndTime { get; set; } = -1;

        public AvailableParts AvailableParts { get; set;}

        public int BandDifficulty { get; set; } = -1;
        public Dictionary<Instrument, int> PartDifficulties { get; set; } = new();

        public ParseSettings ParseSettings { get; set; } = ParseSettings.Default;

        public SongMetadata() { }
    }
}