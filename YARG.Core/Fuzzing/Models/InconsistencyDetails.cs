using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.Models
{
    /// <summary>
    /// Details about an inconsistency found during fuzzing.
    /// </summary>
    public class InconsistencyDetails
    {
        /// <summary>Name of the property that was inconsistent</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Expected value</summary>
        public object? ExpectedValue { get; set; }

        /// <summary>Actual value that was found</summary>
        public object? ActualValue { get; set; }

        /// <summary>Numerical difference between expected and actual values</summary>
        public double NumericalDifference { get; set; }

        /// <summary>Relative error as a percentage</summary>
        public double RelativeError { get; set; }

        /// <summary>Frame timing pattern that caused the inconsistency</summary>
        public FrameTimingPattern ProblematicPattern { get; set; }

        /// <summary>Input sequence that was relevant to the inconsistency</summary>
        public GameInput[] RelevantInputs { get; set; } = System.Array.Empty<GameInput>();

        /// <summary>Critical frame times where the inconsistency occurred</summary>
        public double[] CriticalFrameTimes { get; set; } = System.Array.Empty<double>();

        /// <summary>Additional context about the inconsistency</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity level of the inconsistency</summary>
        public InconsistencySeverity Severity { get; set; } = InconsistencySeverity.Medium;
    }

    /// <summary>
    /// Severity levels for inconsistencies.
    /// </summary>
    public enum InconsistencySeverity
    {
        /// <summary>Minor inconsistency that may not affect gameplay</summary>
        Low,
        
        /// <summary>Moderate inconsistency that could affect gameplay</summary>
        Medium,
        
        /// <summary>Major inconsistency that significantly affects gameplay</summary>
        High,
        
        /// <summary>Critical inconsistency that breaks gameplay</summary>
        Critical
    }
}