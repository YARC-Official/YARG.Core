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
        public string Name { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public string Year { get; set; }

        public bool IsMaster { get; set; }

        public string Charter { get; set; }
        public string Source { get; set; }

        public string LoadingPhrase { get; set; }

        public int AlbumTrack { get; set; }
        public int PlaylistTrack { get; set; }

        public double SongLength { get; set; }

        public double ChartOffset { get; set; }

        public double PreviewStart { get; set; }
        public double PreviewEnd { get; set; }

        public double VideoStartTime { get; set; }
        public double VideoEndTime { get; set; }

        public int BandDifficulty { get; set; }
        public Dictionary<Instrument, int> PartDifficulties { get; set; } = new();

        public ParseSettings ParseSettings { get; set; }

        public SongMetadata() { }
    }
}