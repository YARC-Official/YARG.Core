using System;
using YARG.Core.Fuzzing.Interfaces;

namespace YARG.Core.Fuzzing.Models
{
    /// <summary>
    /// Result of a fuzzer test execution.
    /// </summary>
    public class FuzzerResult
    {
        /// <summary>The test case that was executed</summary>
        public FuzzerTestCase TestCase { get; set; } = new();

        /// <summary>Whether the test passed (no inconsistencies found)</summary>
        public bool Passed { get; set; }

        /// <summary>Array of inconsistencies found during testing</summary>
        public InconsistencyDetails[] Inconsistencies { get; set; } = System.Array.Empty<InconsistencyDetails>();

        /// <summary>Minimal reproduction case (if minimization was enabled)</summary>
        public MinimizedTestCase? MinimalReproduction { get; set; }

        /// <summary>Total execution time for the test</summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>Number of individual executions performed</summary>
        public int TotalExecutions { get; set; }

        /// <summary>Detailed report of the test results</summary>
        public string DetailedReport { get; set; } = string.Empty;

        /// <summary>Exception that occurred during testing (if any)</summary>
        public Exception? Exception { get; set; }

        /// <summary>Whether the test was cancelled due to timeout</summary>
        public bool TimedOut { get; set; }

        /// <summary>Statistical information about the test run</summary>
        public FuzzerStatistics Statistics { get; set; } = new();

        /// <summary>CLI command to reproduce this test failure</summary>
        public string ReproductionCommand { get; set; } = string.Empty;
    }

    /// <summary>
    /// Statistical information about a fuzzer test run.
    /// </summary>
    public class FuzzerStatistics
    {
        /// <summary>Average execution time per iteration</summary>
        public TimeSpan AverageIterationTime { get; set; }

        /// <summary>Minimum execution time observed</summary>
        public TimeSpan MinExecutionTime { get; set; }

        /// <summary>Maximum execution time observed</summary>
        public TimeSpan MaxExecutionTime { get; set; }

        /// <summary>Number of frame timing patterns tested</summary>
        public int FrameTimingPatternsCount { get; set; }

        /// <summary>Number of input sequences tested</summary>
        public int InputSequencesCount { get; set; }

        /// <summary>Total number of engine state comparisons performed</summary>
        public int StateComparisonsCount { get; set; }

        /// <summary>Memory usage peak during testing</summary>
        public long PeakMemoryUsage { get; set; }
    }
}