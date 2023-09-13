// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Globalization;
using System.IO;
using YARG.Core.Utility;

namespace MoonscraperChartEditor.Song.IO
{
    using TrimSplitter = SpanSplitter<char, TrimSplitProcessor>;

    internal static class ChartMetadata
    {
        // .chart only allows floating point values that use periods for decimal points
        public static readonly CultureInfo FormatCulture = new("en-US");

        public const string NAME_KEY = "Name";
        public const string ARTIST_KEY = "Artist";
        public const string ALBUM_KEY = "Album";
        public const string GENRE_KEY = "Genre";
        public const string YEAR_KEY = "Year";
        public const string CHARTER_KEY = "Charter";
        public const string DIFFICULTY_KEY = "Difficulty";
        public const string LENGTH_KEY = "Length";
        public const string OFFSET_KEY = "Offset";
        public const string PREVIEW_START_KEY = "PreviewStart";
        public const string PREVIEW_END_KEY = "PreviewEnd";
        public const string RESOLUTION_KEY = "Resolution";

        public static void ParseSongSection(MoonSong song, TrimSplitter sectionLines)
        {
            var metadata = song.metaData = new Metadata();

            foreach (var line in sectionLines)
            {
                var key = line.SplitOnceTrimmed('=', out var value);
                value = value.Trim('"'); // Strip off any quotation marks

                // Name = "5000 Robots"
                if (key.Equals(NAME_KEY, StringComparison.Ordinal))
                    metadata.name = ParseString(value);

                // Artist = "TheEruptionOffer"
                else if (key.Equals(ARTIST_KEY, StringComparison.Ordinal))
                    metadata.artist = ParseString(value);

                // Album = "Rockman Holic"
                else if (key.Equals(ALBUM_KEY, StringComparison.Ordinal))
                    metadata.album = ParseString(value);

                // Genre = "rock"
                else if (key.Equals(GENRE_KEY, StringComparison.Ordinal))
                    metadata.genre = ParseString(value);

                // Year = ", 2023"
                else if (key.Equals(YEAR_KEY, StringComparison.Ordinal))
                    metadata.year = ParseYear(value);

                // Charter = "TheEruptionOffer"
                else if (key.Equals(CHARTER_KEY, StringComparison.Ordinal))
                    metadata.charter = ParseString(value);

                // Difficulty = 0
                else if (key.Equals(DIFFICULTY_KEY, StringComparison.Ordinal))
                    metadata.difficulty = ParseInteger(value);

                // Length = 300
                else if (key.Equals(LENGTH_KEY, StringComparison.Ordinal))
                    song.manualLength = ParseFloat(value);

                // PreviewStart = 0.00
                else if (key.Equals(PREVIEW_START_KEY, StringComparison.Ordinal))
                    metadata.previewStart = ParseFloat(value);

                // PreviewEnd = 0.00
                else if (key.Equals(PREVIEW_END_KEY, StringComparison.Ordinal))
                    metadata.previewEnd = ParseFloat(value);

                // Offset = 0
                else if (key.Equals(OFFSET_KEY, StringComparison.Ordinal))
                    song.offset = ParseFloat(value);

                // Resolution = 192
                else if (key.Equals(RESOLUTION_KEY, StringComparison.Ordinal))
                {
                    int resolution = ParseInteger(value);
                    if (resolution < 1)
                        throw new InvalidDataException($"Invalid .chart resolution {resolution}! Must be at least 1\nLine text: {line.ToString()}");
                    song.resolution = resolution;
                }
            }
        }

        private static string ParseString(ReadOnlySpan<char> valueString)
        {
            return valueString.ToString();
        }

        private static string ParseYear(ReadOnlySpan<char> valueString)
        {
            return valueString.Trim(',').Trim().ToString();
        }

        private static int ParseInteger(ReadOnlySpan<char> valueString, int defaultValue = -1)
        {
            return int.TryParse(valueString, out int value) ? value : defaultValue;
        }

        private static float ParseFloat(ReadOnlySpan<char> valueString, float defaultValue = 0f)
        {
            return float.TryParse(valueString, NumberStyles.Float, FormatCulture, out float value) ? value : defaultValue;
        }
    }
}