// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Globalization;
using System.Text.RegularExpressions;

namespace MoonscraperChartEditor.Song.IO
{
    internal static class ChartMetadata
    {
        private const string QUOTEVALIDATE = @"""[^""\\]*(?:\\.[^""\\]*)*""";
        private const string QUOTESEARCH = "\"([^\"]*)\"";
        private const string FLOATSEARCH = @"[\-\+]?\d+(\.\d+)?";       // US culture only

        public static readonly CultureInfo FormatCulture = new("en-US");

        public enum MetadataValueType
        {
            String,
            Float,
            Player2,
            Difficulty,
            Year,
        }

        public class MetadataItem
        {
            private readonly string m_key;
            private readonly Regex m_readerParseRegex;

            public string key => m_key;
            public Regex regex => m_readerParseRegex;

            public MetadataItem(string key, MetadataValueType type)
            {
                m_key = key;
                m_readerParseRegex = type switch
                {
                    MetadataValueType.String => new Regex(key + " = " + QUOTEVALIDATE, RegexOptions.Compiled),
                    MetadataValueType.Float => new Regex(key + " = " + FLOATSEARCH, RegexOptions.Compiled),
                    MetadataValueType.Player2 => new Regex(key + @" = \w+", RegexOptions.Compiled),
                    MetadataValueType.Difficulty => new Regex(key + @" = \d+", RegexOptions.Compiled),
                    MetadataValueType.Year => new Regex(key + " = " + QUOTEVALIDATE, RegexOptions.Compiled),
                    _ => throw new System.Exception("Unhandled Metadata item type")
                };
            }
        }

        public static readonly MetadataItem name = new("Name", MetadataValueType.String);
        public static readonly MetadataItem artist = new("Artist", MetadataValueType.String);
        public static readonly MetadataItem charter = new("Charter", MetadataValueType.String);
        public static readonly MetadataItem offset = new("Offset", MetadataValueType.Float);
        public static readonly MetadataItem resolution = new("Resolution", MetadataValueType.Float);
        public static readonly MetadataItem difficulty = new("Difficulty", MetadataValueType.Difficulty);
        public static readonly MetadataItem length = new("Length", MetadataValueType.Float);
        public static readonly MetadataItem previewStart = new("PreviewStart", MetadataValueType.Float);
        public static readonly MetadataItem previewEnd = new("PreviewEnd", MetadataValueType.Float);
        public static readonly MetadataItem genre = new("Genre", MetadataValueType.String);
        public static readonly MetadataItem year = new("Year", MetadataValueType.Year);
        public static readonly MetadataItem album = new("Album", MetadataValueType.String);

        public static string ParseAsString(string line)
        {
            return Regex.Matches(line, QUOTESEARCH)[0].ToString().Trim('"');
        }

        public static float ParseAsFloat(string line)
        {
            return float.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString(), FormatCulture);  // .chart format only allows '.' as decimal seperators. Need to parse correctly under any locale.
        }

        public static short ParseAsShort(string line)
        {
            return short.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString());
        }
    }
}