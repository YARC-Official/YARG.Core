using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Chart;
using YARG.Core.Song;

namespace YARG.Core.Benchmarks
{
    // [SimpleJob(RunStrategy.ColdStart, targetCount: 25, invocationCount: 1)]
    public class MidiParsingBenchmarks
    {
        public static string ChartPath { get; set; }

        private ParseSettings settings = ParseSettings.Default;
        private MidiFile midi;

        [GlobalSetup]
        public void Initialize()
        {
            midi = MidiFile.Read(ChartPath);
        }

        [Benchmark]
        public SongChart ChartParsing()
        {
            return SongChart.FromMidi(settings, midi);
        }
    }
}
