using System;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        /// <summary>
        /// Uses the current instrument to institute applicable test parameters.
        /// This does not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public DrumsType ParseChart<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader, DrumsType drumType)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : StringDecoder<TChar>, new()
        {
            DrumPreparseHandler drums = new(drumType);
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument())
                    reader.SkipTrack();
                else if (reader.Instrument != NoteTracks_Chart.Drums)
                    ParseChartTrack(reader);
                else
                    drums.ParseChart(reader);
            }

            SetDrums(drums);
            return drums.Type;
        }

        private void ParseChartTrack<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : StringDecoder<TChar>, new()
        {
            bool skip = reader.Instrument switch
            {
                NoteTracks_Chart.Single =>       ChartPreparser.Preparse(reader, ref FiveFretGuitar,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleBass =>   ChartPreparser.Preparse(reader, ref FiveFretBass,       ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleRhythm => ChartPreparser.Preparse(reader, ref FiveFretRhythm,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleGuitar => ChartPreparser.Preparse(reader, ref FiveFretCoopGuitar, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.GHLGuitar =>    ChartPreparser.Preparse(reader, ref SixFretGuitar,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLBass =>      ChartPreparser.Preparse(reader, ref SixFretBass,        ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLRhythm =>    ChartPreparser.Preparse(reader, ref SixFretRhythm,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLCoop =>      ChartPreparser.Preparse(reader, ref SixFretCoopGuitar,  ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.Keys =>         ChartPreparser.Preparse(reader, ref Keys,               ChartPreparser.ValidateFiveFret),
                _ => true,
            };

            if (skip)
                reader.SkipTrack();
        }
    }
}
