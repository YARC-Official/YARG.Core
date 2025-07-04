using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.Models
{
    /// <summary>
    /// Represents a single test case for the fuzzer.
    /// </summary>
    public class FuzzerTestCase
    {
        /// <summary>Name of the test case</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Song chart to test with</summary>
        public SongChart? Chart { get; set; }

        /// <summary>Target instrument</summary>
        public Instrument Instrument { get; set; }

        /// <summary>Target difficulty</summary>
        public Difficulty Difficulty { get; set; }

        /// <summary>Input sequence to execute</summary>
        public GameInput[] InputSequence { get; set; } = System.Array.Empty<GameInput>();

        /// <summary>Frame timing patterns to test</summary>
        public FrameTimingPattern[] FrameTimingPatterns { get; set; } = System.Array.Empty<FrameTimingPattern>();

        /// <summary>Engine parameters for the test</summary>
        public BaseEngineParameters? EngineParameters { get; set; }

        /// <summary>Random seed for reproducibility</summary>
        public int RandomSeed { get; set; }

        /// <summary>Start time for the test in seconds</summary>
        public double StartTime { get; set; }

        /// <summary>End time for the test in seconds</summary>
        public double EndTime { get; set; }

        /// <summary>Whether this test case focuses on star power mechanics</summary>
        public bool IsStarPowerFocused { get; set; }

        /// <summary>Expected duration of the test case</summary>
        public double ExpectedDuration => EndTime - StartTime;
    }
}