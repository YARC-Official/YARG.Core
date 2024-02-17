using System;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal static partial class DotChartParser
    {
        private struct DotChartMetadata
        {
            public uint Resolution;
        }

        private const string RESOLUTION = "Resolution";

        private static DotChartMetadata ParseMetadata(AsciiTrimSplitter section)
        {
            var metadata = new DotChartMetadata();

            foreach (var line in section)
            {
                var key = line.SplitOnceTrimmedAscii('=', out var value);
                value = value.TrimOnce('"');

                if (key.Equals(RESOLUTION, StringComparison.OrdinalIgnoreCase))
                {
                    if (!uint.TryParse(value, out uint resolution))
                        throw new Exception($"Failed to parse resolution text: {value.ToString()}");

                    metadata.Resolution = resolution;

                    // NOTE: Remove if any additional values need to be parsed
                    break;
                }
            }

            return metadata;
        }
    }
}