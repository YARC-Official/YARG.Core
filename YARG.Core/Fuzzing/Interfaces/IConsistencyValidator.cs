using YARG.Core.Engine;
using YARG.Core.Fuzzing.Models;

namespace YARG.Core.Fuzzing.Interfaces
{
    /// <summary>
    /// Interface for validating consistency between engine states.
    /// </summary>
    public interface IConsistencyValidator
    {
        /// <summary>
        /// Validates consistency across multiple engine states.
        /// </summary>
        /// <param name="engineStates">Array of engine states to compare</param>
        /// <param name="tolerance">Tolerance for floating-point comparisons</param>
        /// <returns>Consistency validation result</returns>
        ConsistencyResult ValidateEngineStates(BaseStats[] engineStates, double tolerance);

        /// <summary>
        /// Checks if two engine states are equivalent within tolerance.
        /// </summary>
        /// <param name="state1">First engine state</param>
        /// <param name="state2">Second engine state</param>
        /// <param name="tolerance">Tolerance for floating-point comparisons</param>
        /// <returns>True if states are equivalent</returns>
        bool AreStatesEquivalent(BaseStats state1, BaseStats state2, double tolerance);

        /// <summary>
        /// Gets detailed information about inconsistencies between two states.
        /// </summary>
        /// <param name="expected">Expected engine state</param>
        /// <param name="actual">Actual engine state</param>
        /// <returns>Detailed inconsistency information</returns>
        InconsistencyDetails GetInconsistencyDetails(BaseStats expected, BaseStats actual);
    }

    /// <summary>
    /// Result of consistency validation.
    /// </summary>
    public struct ConsistencyResult
    {
        /// <summary>Whether all states are consistent</summary>
        public bool IsConsistent;
        
        /// <summary>Array of detected inconsistencies</summary>
        public InconsistencyDetails[] Inconsistencies;
        
        /// <summary>Maximum deviation found</summary>
        public double MaxDeviation;
    }
}