using System;
using YARG.Core.Chart;

namespace YARG.Core.Fuzzing.Models
{
    /// <summary>
    /// Configuration settings for the fuzzer engine.
    /// </summary>
    public class FuzzerConfiguration
    {
        /// <summary>Number of test iterations per scenario</summary>
        public int TestIterationsPerScenario { get; set; } = 100;

        /// <summary>Minimum frame time in seconds (~120 FPS)</summary>
        public double MinFrameTime { get; set; } = 0.008;

        /// <summary>Maximum frame time in seconds (~30 FPS)</summary>
        public double MaxFrameTime { get; set; } = 0.033;

        /// <summary>Tolerance for floating-point comparisons</summary>
        public double FloatingPointTolerance { get; set; } = 1e-10;



        /// <summary>Whether to enable test case minimization</summary>
        public bool EnableTestCaseMinimization { get; set; } = true;

        /// <summary>Maximum duration for a single test run</summary>
        public TimeSpan MaxTestDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>Target instruments for testing</summary>
        public string[] TargetInstruments { get; set; } = { "FiveFretGuitar", "FourLaneDrums" };

        /// <summary>Target difficulties for testing</summary>
        public Difficulty[] TargetDifficulties { get; set; } = { Difficulty.Expert };

        /// <summary>Random seed for reproducible test generation</summary>
        public int RandomSeed { get; set; } = Environment.TickCount;

        /// <summary>Whether to enable parallel test execution</summary>
        public bool EnableParallelExecution { get; set; } = true;

        /// <summary>Maximum number of parallel threads to use</summary>
        public int MaxParallelThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>Chart coverage strategy for testing</summary>
        public ChartCoverageStrategy CoverageStrategy { get; set; } = ChartCoverageStrategy.FullChart;

        /// <summary>Maximum chart duration to test in seconds (0 for no limit)</summary>
        public double MaxChartDuration { get; set; } = 0.0;

        /// <summary>Time window size for windowed testing strategy in seconds</summary>
        public double TimeWindowSize { get; set; } = 60.0;

        /// <summary>Number of samples for sampled testing strategy</summary>
        public int SampleCount { get; set; } = 10;
    }

    /// <summary>
    /// Strategy for chart coverage during testing.
    /// </summary>
    public enum ChartCoverageStrategy
    {
        /// <summary>Test the entire chart duration</summary>
        FullChart,
        
        /// <summary>Test only a specified time window from the start</summary>
        TimeWindowed,
        
        /// <summary>Test multiple sampled segments throughout the chart</summary>
        Sampled
    }
}