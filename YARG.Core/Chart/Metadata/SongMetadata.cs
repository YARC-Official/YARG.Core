using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The metadata for a song.
    /// </summary>
    public partial class SongMetadata
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
    }
}