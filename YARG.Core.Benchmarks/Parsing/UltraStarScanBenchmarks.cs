using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;

namespace YARG.Core.Benchmarks
{
    /// <summary>
    /// What a library scan does with one UltraStar .txt: decide it's a chart, then read the
    /// tags and voice count it needs.
    /// </summary>
    [MemoryDiagnoser]
    public class UltraStarScanBenchmarks
    {
        private string _chartPath;
        private FixedArray<byte> _chart;

        [GlobalSetup]
        public void Initialize()
        {
            _chartPath = Environment.GetEnvironmentVariable(Program.CHART_PATH_VAR)
                ?? throw new Exception("Could not find chart path environment variable!");
            _chart = FixedArray.LoadFile(_chartPath);
        }

        [GlobalCleanup]
        public void Cleanup() => _chart.Dispose();

        [Benchmark(Baseline = true, Description = "Full loader (previous scan)")]
        public int FullLoader() => new UltraStarLoader(_chart).VoiceCount;

        [Benchmark(Description = "Header scan")]
        public int HeaderScan() => UltraStarLoader.ScanHeader(_chart).VoiceCount;

        [Benchmark(Description = "Probe + second read (previous discovery)")]
        public int ProbeThenLoad()
        {
            using (var reader = new FileInfo(_chartPath).OpenText())
            {
                string line;
                while ((line = reader.ReadLine()) != null && line.Trim().Length == 0)
                {
                }
            }
            using var file = FixedArray.LoadFile(_chartPath);
            return file.Length;
        }

        [Benchmark(Description = "Single read + classify")]
        public int ReadOnceAndClassify()
        {
            using var stream = new FileStream(_chartPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            UltraStarLoader.ClassifyTextFile(stream);
            stream.Position = 0;
            using var file = FixedArray.Read(stream, stream.Length);
            return file.Length;
        }
    }
}
