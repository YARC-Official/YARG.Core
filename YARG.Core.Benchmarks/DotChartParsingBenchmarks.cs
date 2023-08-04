using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using YARG.Core.Chart;
using YARG.Core.Song;

namespace YARG.Core.Benchmarks
{
    // [SimpleJob(RunStrategy.ColdStart, targetCount: 25, invocationCount: 1)]
    public class DotChartParsingBenchmarks
    {
        public static string ChartPath { get; set; }

        private ParseSettings settings = ParseSettings.Default;
        private string chartText;

        [GlobalSetup]
        public void Initialize()
        {
            chartText = File.ReadAllText(ChartPath);
        }

        [Benchmark]
        public SongChart ChartParsing()
        {
            return SongChart.FromDotChart(settings, chartText);
        }
    }
}
