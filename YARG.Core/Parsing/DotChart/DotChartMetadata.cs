using System;
using System.Globalization;

namespace YARG.Core.Parsing
{
    /// <summary>
    /// Metadata contained in a .chart file.
    /// </summary>
    public ref struct DotChartMetadata
    {
        // .chart only allows floating point values that use periods for decimal points
        public static readonly CultureInfo FormatCulture = new("en-US");

        public const string RESOLUTION_KEY = "Resolution";
        public const string HOPO_FACTOR_KEY = "HoPo";

        public const string NAME_KEY = "Name";
        public const string ARTIST_KEY = "Artist";
        public const string ALBUM_KEY = "Album";
        public const string GENRE_KEY = "Genre";
        public const string YEAR_KEY = "Year";

        public const string CHARTER_KEY = "Charter";
        public const string DIFFICULTY_KEY = "Difficulty";

        public const string OFFSET_KEY = "Offset";

        public const string PREVIEW_START_KEY = "PreviewStart";
        public const string PREVIEW_END_KEY = "PreviewEnd";

        public const string MUSIC_STREAM_KEY = "MusicStream";
        public const string GUITAR_STREAM_KEY = "GuitarStream";
        public const string RHYTHM_STREAM_KEY = "RhythmStream";
        public const string BASS_STREAM_KEY = "BassStream";
        public const string KEYS_STREAM_KEY = "KeysStream";
        public const string DRUM_STREAM_KEY = "DrumStream";
        public const string DRUM2_STREAM_KEY = "Drum2Stream";
        public const string DRUM3_STREAM_KEY = "Drum3Stream";
        public const string DRUM4_STREAM_KEY = "Drum4Stream";
        public const string VOCAL_STREAM_KEY = "VocalStream";
        public const string CROWD_STREAM_KEY = "CrowdStream";

        // With a 192 resolution, .chart has a HOPO threshold of 65 ticks, not 64,
        // so we need to scale this factor to different resolutions (480 res = 162.5 -> 162 threshold)
        // This extra tick is meant for some slight leniency, .mid has it too but it's applied *after*
        // factoring in the resolution there, not before
        public const float BASE_HOPO_RESOLUTION = 192;
        public const float BASE_HOPO_THRESHOLD = (BASE_HOPO_RESOLUTION / 3) + 1;
        public const float HOPO_RESOLUTION_FACTOR = BASE_HOPO_THRESHOLD / BASE_HOPO_RESOLUTION;

        public uint Resolution;
        public float HopoFactor;

        public readonly uint HopoThreshold
        {
            get
            {
                if (HopoFactor > 0)
                {
                    // The HOPO factor is equivalent to the HOPO threshold's
                    // step size denominator divided by 4
                    // 1/4 step = 1.0, 1/8 = 2.0, 1/2 = 0.5
                    float stepDenominator = 4 * HopoFactor;
                    return (uint)(Resolution / stepDenominator);
                }

                return (uint)(HOPO_RESOLUTION_FACTOR * Resolution);
            }
        }

        public ReadOnlySpan<char> Name;
        public ReadOnlySpan<char> Artist;
        public ReadOnlySpan<char> Album;
        public ReadOnlySpan<char> Genre;
        public ReadOnlySpan<char> Year;

        public ReadOnlySpan<char> Charter;
        public int Difficulty;

        public double Offset;

        public double PreviewStart;
        public double PreviewEnd;

        public ReadOnlySpan<char> MusicStream;
        public ReadOnlySpan<char> GuitarStream;
        public ReadOnlySpan<char> RhythmStream;
        public ReadOnlySpan<char> BassStream;
        public ReadOnlySpan<char> KeysStream;
        public ReadOnlySpan<char> DrumStream;
        public ReadOnlySpan<char> Drum2Stream;
        public ReadOnlySpan<char> Drum3Stream;
        public ReadOnlySpan<char> Drum4Stream;
        public ReadOnlySpan<char> VocalStream;
        public ReadOnlySpan<char> CrowdStream;

        public static DotChartMetadata ParseSongSection(DotChartSection section)
        {
            var metadata = new DotChartMetadata();

            foreach (var line in section)
            {
                var key = line.Key;
                var value = line.Value.Trim('"'); // Strip off any quotation marks

                // Resolution = 192
                if (key.Equals(RESOLUTION_KEY, StringComparison.Ordinal))
                    metadata.Resolution = ParseUInt32(value);

                // HoPo = 2.00
                else if (key.Equals(HOPO_FACTOR_KEY, StringComparison.Ordinal))
                    metadata.HopoFactor = ParseFloat(value);

                // Name = "5000 Robots"
                else if (key.Equals(NAME_KEY, StringComparison.Ordinal))
                    metadata.Name = ParseString(value);

                // Artist = "TheEruptionOffer"
                else if (key.Equals(ARTIST_KEY, StringComparison.Ordinal))
                    metadata.Artist = ParseString(value);

                // Album = "Rockman Holic"
                else if (key.Equals(ALBUM_KEY, StringComparison.Ordinal))
                    metadata.Album = ParseString(value);

                // Genre = "rock"
                else if (key.Equals(GENRE_KEY, StringComparison.Ordinal))
                    metadata.Genre = ParseString(value);

                // Year = ", 2023"
                else if (key.Equals(YEAR_KEY, StringComparison.Ordinal))
                    metadata.Year = ParseString(value.TrimStart(','));

                // Charter = "TheEruptionOffer"
                else if (key.Equals(CHARTER_KEY, StringComparison.Ordinal))
                    metadata.Charter = ParseString(value);

                // Difficulty = 0
                else if (key.Equals(DIFFICULTY_KEY, StringComparison.Ordinal))
                    metadata.Difficulty = ParseInt32(value);

                // Offset = 0
                else if (key.Equals(OFFSET_KEY, StringComparison.Ordinal))
                    metadata.Offset = ParseFloat(value);

                // PreviewStart = 0.00
                else if (key.Equals(PREVIEW_START_KEY, StringComparison.Ordinal))
                    metadata.PreviewStart = ParseFloat(value);

                // PreviewEnd = 0.00
                else if (key.Equals(PREVIEW_END_KEY, StringComparison.Ordinal))
                    metadata.PreviewEnd = ParseFloat(value, defaultValue: -1);

                // MusicStream = "song.ogg"
                else if (key.Equals(MUSIC_STREAM_KEY, StringComparison.Ordinal))
                    metadata.MusicStream = ParseString(value);

                // GuitarStream = "guitar.ogg"
                else if (key.Equals(GUITAR_STREAM_KEY, StringComparison.Ordinal))
                    metadata.GuitarStream = ParseString(value);

                // RhythmStream = "rhythm.ogg"
                else if (key.Equals(RHYTHM_STREAM_KEY, StringComparison.Ordinal))
                    metadata.RhythmStream = ParseString(value);

                // BassStream = "bass.ogg"
                else if (key.Equals(BASS_STREAM_KEY, StringComparison.Ordinal))
                    metadata.BassStream = ParseString(value);

                // KeysStream = "keys.ogg"
                else if (key.Equals(KEYS_STREAM_KEY, StringComparison.Ordinal))
                    metadata.KeysStream = ParseString(value);

                // DrumStream = "drums_1.ogg"
                else if (key.Equals(DRUM_STREAM_KEY, StringComparison.Ordinal))
                    metadata.DrumStream = ParseString(value);

                // Drum2Stream = "drums_2.ogg"
                else if (key.Equals(DRUM2_STREAM_KEY, StringComparison.Ordinal))
                    metadata.Drum2Stream = ParseString(value);

                // Drum3Stream = "drums_3.ogg"
                else if (key.Equals(DRUM3_STREAM_KEY, StringComparison.Ordinal))
                    metadata.Drum3Stream = ParseString(value);

                // Drum4Stream = "drums_4.ogg"
                else if (key.Equals(DRUM4_STREAM_KEY, StringComparison.Ordinal))
                    metadata.Drum4Stream = ParseString(value);

                // VocalStream = "vocals.ogg"
                else if (key.Equals(VOCAL_STREAM_KEY, StringComparison.Ordinal))
                    metadata.VocalStream = ParseString(value);

                // CrowdStream = "crowd.ogg"
                else if (key.Equals(CROWD_STREAM_KEY, StringComparison.Ordinal))
                    metadata.CrowdStream = ParseString(value);
            }

            return metadata;
        }

        private static string ParseString(ReadOnlySpan<char> valueString)
        {
            return valueString.Trim().ToString();
        }

        private static int ParseInt32(ReadOnlySpan<char> valueString, int defaultValue = -1)
        {
            return int.TryParse(valueString, out int value) ? value : defaultValue;
        }

        private static uint ParseUInt32(ReadOnlySpan<char> valueString, uint defaultValue = 0)
        {
            return uint.TryParse(valueString, out uint value) ? value : defaultValue;
        }

        private static float ParseFloat(ReadOnlySpan<char> valueString, float defaultValue = 0f)
        {
            return float.TryParse(valueString, NumberStyles.Float, FormatCulture, out float value) ? value : defaultValue;
        }
    }
}