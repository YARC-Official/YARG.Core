using System;

namespace YARG.Core.Song
{
    public enum RbVocalGender : byte
    {
        Female,
        Male,
        Unspecified,
    };

    public enum SongTonality : byte
    {
        Major,
        Minor,
        Unspecified,
    };

    public enum EncodingType : byte
    {
        ASCII,
        Latin1,
        UTF8,
        UTF16,
        UTF32
    };

    public struct RBMetadata
    {
        public static readonly RBMetadata Default = new()
        {
            SongID = string.Empty,
            DrumBank = string.Empty,
            VocalPercussionBank = string.Empty,
            AnimTempo = 0,
            VocalSongScrollSpeed = 0,
            VocalTonicNote = 0,
            VenueVersion = 0,
            TuningOffsetCents = 0,
            RbVocalGender = RbVocalGender.Unspecified,
            SongTonality = SongTonality.Unspecified,
            Soloes = Array.Empty<string>(),
            VideoVenues = Array.Empty<string>(),
            RealGuitarTuning = Array.Empty<int>(),
            RealBassTuning = Array.Empty<int>(),
            MidiEncoding = EncodingType.Latin1
        };

        public string SongID;
        public string DrumBank;
        public string VocalPercussionBank;

        public uint AnimTempo;
        public uint VocalSongScrollSpeed;
        public uint VocalTonicNote;
        public uint VenueVersion;
        public int  TuningOffsetCents;

        public RbVocalGender RbVocalGender;
        public SongTonality SongTonality;

        public string[] Soloes;
        public string[] VideoVenues;

        public int[] RealGuitarTuning;
        public int[] RealBassTuning;

        public EncodingType MidiEncoding;
    }
}
