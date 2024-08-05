using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song
{
    public struct RBMetadata
    {
        public static readonly RBMetadata Default = new()
        {
            SongID = string.Empty,
            DrumBank = string.Empty,
            VocalPercussionBank = string.Empty,
            VocalGender = true,
            Soloes = Array.Empty<string>(),
            VideoVenues = Array.Empty<string>(),
            RealGuitarTuning = Array.Empty<int>(),
            RealBassTuning = Array.Empty<int>(),
            Indices = RBAudio<int>.Empty,
            Panning = RBAudio<float>.Empty,
        };

        public string SongID;
        public uint AnimTempo;
        public string DrumBank;
        public string VocalPercussionBank;
        public uint VocalSongScrollSpeed;
        public bool VocalGender; //true for male, false for female
        //public bool HasAlbumArt;
        //public bool IsFake;
        public uint VocalTonicNote;
        public bool SongTonality; // 0 = major, 1 = minor
        public int TuningOffsetCents;
        public uint VenueVersion;

        public string[] Soloes;
        public string[] VideoVenues;

        public int[] RealGuitarTuning;
        public int[] RealBassTuning;

        public RBAudio<int> Indices;
        public RBAudio<float> Panning;
    }
}
