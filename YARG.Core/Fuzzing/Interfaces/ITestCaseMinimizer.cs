using System;
using YARG.Core.Input;
using YARG.Core.Fuzzing.Models;

namespace YARG.Core.Fuzzing.Interfaces
{
    /// <summary>
    /// Interface for minimizing test cases to find minimal reproduction scenarios.
    /// </summary>
    public interface ITestCaseMinimizer
    {
        /// <summary>
        /// Minimizes a test case to the smallest reproduction scenario.
        /// </summary>
        /// <param name="originalCase">Original failing test case</param>
        /// <param name="inconsistency">The inconsistency to reproduce</param>
        /// <returns>Minimized test case</returns>
        MinimizedTestCase MinimizeTestCase(FuzzerTestCase originalCase, InconsistencyDetails inconsistency);

        /// <summary>
        /// Minimizes an input sequence to the shortest sequence that reproduces an issue.
        /// </summary>
        /// <param name="inputs">Original input sequence</param>
        /// <param name="reproducesIssue">Function to test if the issue is reproduced</param>
        /// <returns>Minimized input sequence</returns>
        GameInput[] MinimizeInputSequence(GameInput[] inputs, Func<GameInput[], bool> reproducesIssue);

        /// <summary>
        /// Minimizes frame timing pattern to the simplest pattern that reproduces an issue.
        /// </summary>
        /// <param name="pattern">Original frame timing pattern</param>
        /// <param name="reproducesIssue">Function to test if the issue is reproduced</param>
        /// <returns>Minimized frame timing pattern</returns>
        FrameTimingPattern MinimizeFrameTiming(FrameTimingPattern pattern, Func<FrameTimingPattern, bool> reproducesIssue);
    }

    /// <summary>
    /// A minimized test case that reproduces an issue with minimal complexity.
    /// </summary>
    public class MinimizedTestCase
    {
        /// <summary>Name of the minimized test case</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Minimized input sequence</summary>
        public GameInput[] MinimalInputs { get; set; } = System.Array.Empty<GameInput>();
        
        /// <summary>Minimized frame timing pattern</summary>
        public FrameTimingPattern MinimalFrameTiming { get; set; }
        
        /// <summary>The inconsistency this case reproduces</summary>
        public InconsistencyDetails ReproducedInconsistency { get; set; } = new();
        
        /// <summary>Step-by-step reproduction instructions</summary>
        public string[] ReproductionSteps { get; set; } = System.Array.Empty<string>();
        
        /// <summary>Original test case this was minimized from</summary>
        public FuzzerTestCase OriginalTestCase { get; set; } = new();
    }
}