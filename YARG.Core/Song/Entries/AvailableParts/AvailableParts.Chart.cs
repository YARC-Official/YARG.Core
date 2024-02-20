using System;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public partial struct AvailableParts
    {
        public void ParseChart<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, DrumPreparseHandler drums)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument())
                    reader.SkipTrack();
                else if (reader.Instrument != NoteTracks_Chart.Drums)
                    ParseChartTrack(reader);
                else
                    drums.ParseChart(reader);
            }
        }

        private void ParseChartTrack<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            bool skip = reader.Instrument switch
            {
                NoteTracks_Chart.Single =>       ChartPreparser.Preparse(reader, ref _fiveFretGuitar,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleBass =>   ChartPreparser.Preparse(reader, ref _fiveFretBass,       ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleRhythm => ChartPreparser.Preparse(reader, ref _fiveFretRhythm,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleGuitar => ChartPreparser.Preparse(reader, ref _fiveFretCoopGuitar, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.GHLGuitar =>    ChartPreparser.Preparse(reader, ref _sixFretGuitar,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLBass =>      ChartPreparser.Preparse(reader, ref _sixFretBass,        ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLRhythm =>    ChartPreparser.Preparse(reader, ref _sixFretRhythm,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLCoop =>      ChartPreparser.Preparse(reader, ref _sixFretCoopGuitar,  ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.Keys =>         ChartPreparser.Preparse(reader, ref _keys,               ChartPreparser.ValidateFiveFret),
                _ => true,
            };

            if (skip)
                reader.SkipTrack();
        }
    }
}
