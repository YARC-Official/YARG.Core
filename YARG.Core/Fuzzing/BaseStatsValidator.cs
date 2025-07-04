using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Engine;
using YARG.Core.Fuzzing.Models;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Comprehensive validator for BaseStats engine state comparison.
    /// </summary>
    public class BaseStatsValidator
    {
        private readonly FloatingPointComparer _floatingPointComparer;
        private readonly Dictionary<string, ValidationRule> _validationRules;

        /// <summary>
        /// Initializes a new instance of BaseStatsValidator.
        /// </summary>
        /// <param name="floatingPointComparer">Floating-point comparer for numeric validations</param>
        public BaseStatsValidator(FloatingPointComparer? floatingPointComparer = null)
        {
            _floatingPointComparer = floatingPointComparer ?? new FloatingPointComparer();
            _validationRules = new Dictionary<string, ValidationRule>();
            InitializeDefaultValidationRules();
        }

        /// <summary>
        /// Validates engine state consistency with comprehensive checks.
        /// </summary>
        /// <param name="expected">Expected engine state</param>
        /// <param name="actual">Actual engine state</param>
        /// <param name="tolerance">Tolerance for floating-point comparisons</param>
        /// <returns>List of validation issues found</returns>
        public List<InconsistencyDetails> ValidateEngineState(BaseStats expected, BaseStats actual, double tolerance)
        {
            var issues = new List<InconsistencyDetails>();

            if (expected == null || actual == null)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "EngineState",
                    ExpectedValue = expected,
                    ActualValue = actual,
                    Description = "One or both engine states are null",
                    Severity = InconsistencySeverity.Critical
                });
                return issues;
            }

            // Validate core scoring properties
            ValidateScoring(expected, actual, tolerance, issues);
            
            // Validate combo and multiplier state
            ValidateComboState(expected, actual, tolerance, issues);
            
            // Validate note tracking
            ValidateNoteTracking(expected, actual, tolerance, issues);
            
            // Validate star power mechanics
            ValidateStarPowerMechanics(expected, actual, tolerance, issues);
            
            // Validate state consistency rules
            ValidateStateConsistency(expected, actual, tolerance, issues);
            
            // Check for state corruption
            DetectStateCorruption(expected, actual, issues);

            return issues;
        }

        /// <summary>
        /// Validates intermediate engine states during execution.
        /// </summary>
        /// <param name="states">Sequence of engine states</param>
        /// <param name="tolerance">Tolerance for comparisons</param>
        /// <returns>List of validation issues</returns>
        public List<InconsistencyDetails> ValidateIntermediateStates(BaseStats[] states, double tolerance)
        {
            var issues = new List<InconsistencyDetails>();

            if (states == null || states.Length < 2)
                return issues;

            for (int i = 1; i < states.Length; i++)
            {
                var previousState = states[i - 1];
                var currentState = states[i];

                // Validate state transitions
                ValidateStateTransition(previousState, currentState, tolerance, issues, i);
            }

            return issues;
        }

        /// <summary>
        /// Validates scoring-related properties.
        /// </summary>
        private void ValidateScoring(BaseStats expected, BaseStats actual, double tolerance, List<InconsistencyDetails> issues)
        {
            ValidateProperty(issues, "CommittedScore", expected.CommittedScore, actual.CommittedScore, tolerance);
            ValidateProperty(issues, "PendingScore", expected.PendingScore, actual.PendingScore, tolerance);
            ValidateProperty(issues, "NoteScore", expected.NoteScore, actual.NoteScore, tolerance);
            ValidateProperty(issues, "SustainScore", expected.SustainScore, actual.SustainScore, tolerance);
            ValidateProperty(issues, "MultiplierScore", expected.MultiplierScore, actual.MultiplierScore, tolerance);
            ValidateProperty(issues, "SoloBonuses", expected.SoloBonuses, actual.SoloBonuses, tolerance);
            ValidateProperty(issues, "StarPowerScore", expected.StarPowerScore, actual.StarPowerScore, tolerance);

            // Validate calculated properties
            if (!_floatingPointComparer.AreEqual(expected.TotalScore, actual.TotalScore, tolerance))
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "TotalScore",
                    ExpectedValue = expected.TotalScore,
                    ActualValue = actual.TotalScore,
                    NumericalDifference = actual.TotalScore - expected.TotalScore,
                    RelativeError = _floatingPointComparer.GetRelativeError(expected.TotalScore, actual.TotalScore),
                    Description = "Total score calculation inconsistency",
                    Severity = InconsistencySeverity.High
                });
            }
        }

        /// <summary>
        /// Validates combo and multiplier state.
        /// </summary>
        private void ValidateComboState(BaseStats expected, BaseStats actual, double tolerance, List<InconsistencyDetails> issues)
        {
            ValidateProperty(issues, "Combo", expected.Combo, actual.Combo, tolerance);
            ValidateProperty(issues, "MaxCombo", expected.MaxCombo, actual.MaxCombo, tolerance);
            ValidateProperty(issues, "ScoreMultiplier", expected.ScoreMultiplier, actual.ScoreMultiplier, tolerance);

            // Validate combo consistency rules
            if (actual.MaxCombo < actual.Combo)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "MaxCombo",
                    ExpectedValue = $">= {actual.Combo}",
                    ActualValue = actual.MaxCombo,
                    Description = "MaxCombo should never be less than current Combo",
                    Severity = InconsistencySeverity.High
                });
            }
        }

        /// <summary>
        /// Validates note tracking properties.
        /// </summary>
        private void ValidateNoteTracking(BaseStats expected, BaseStats actual, double tolerance, List<InconsistencyDetails> issues)
        {
            ValidateProperty(issues, "NotesHit", expected.NotesHit, actual.NotesHit, tolerance);
            ValidateProperty(issues, "TotalNotes", expected.TotalNotes, actual.TotalNotes, tolerance);

            // Validate calculated properties
            if (expected.NotesMissed != actual.NotesMissed)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "NotesMissed",
                    ExpectedValue = expected.NotesMissed,
                    ActualValue = actual.NotesMissed,
                    NumericalDifference = actual.NotesMissed - expected.NotesMissed,
                    Description = "Notes missed calculation inconsistency",
                    Severity = InconsistencySeverity.Medium
                });
            }

            // Validate percentage calculation
            if (!_floatingPointComparer.AreEqual(expected.Percent, actual.Percent, tolerance))
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "Percent",
                    ExpectedValue = expected.Percent,
                    ActualValue = actual.Percent,
                    NumericalDifference = actual.Percent - expected.Percent,
                    RelativeError = _floatingPointComparer.GetRelativeError(expected.Percent, actual.Percent),
                    Description = "Hit percentage calculation inconsistency",
                    Severity = InconsistencySeverity.Medium
                });
            }
        }

        /// <summary>
        /// Validates star power mechanics.
        /// </summary>
        private void ValidateStarPowerMechanics(BaseStats expected, BaseStats actual, double tolerance, List<InconsistencyDetails> issues)
        {
            ValidateProperty(issues, "StarPowerTickAmount", expected.StarPowerTickAmount, actual.StarPowerTickAmount, tolerance);
            ValidateProperty(issues, "TotalStarPowerTicks", expected.TotalStarPowerTicks, actual.TotalStarPowerTicks, tolerance);
            ValidateProperty(issues, "StarPowerWhammyTicks", expected.StarPowerWhammyTicks, actual.StarPowerWhammyTicks, tolerance);
            ValidateProperty(issues, "StarPowerActivationCount", expected.StarPowerActivationCount, actual.StarPowerActivationCount, tolerance);
            ValidateProperty(issues, "TimeInStarPower", expected.TimeInStarPower, actual.TimeInStarPower, tolerance);
            ValidateProperty(issues, "IsStarPowerActive", expected.IsStarPowerActive, actual.IsStarPowerActive, tolerance);
            ValidateProperty(issues, "StarPowerPhrasesHit", expected.StarPowerPhrasesHit, actual.StarPowerPhrasesHit, tolerance);
            ValidateProperty(issues, "TotalStarPowerPhrases", expected.TotalStarPowerPhrases, actual.TotalStarPowerPhrases, tolerance);

            // Special validation for star power calculations that may accumulate floating-point errors
            if (!_floatingPointComparer.AreStarPowerValuesEqual(expected.TotalStarPowerBarsFilled, actual.TotalStarPowerBarsFilled, expected.StarPowerActivationCount + 1))
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "TotalStarPowerBarsFilled",
                    ExpectedValue = expected.TotalStarPowerBarsFilled,
                    ActualValue = actual.TotalStarPowerBarsFilled,
                    NumericalDifference = actual.TotalStarPowerBarsFilled - expected.TotalStarPowerBarsFilled,
                    RelativeError = _floatingPointComparer.GetRelativeError(expected.TotalStarPowerBarsFilled, actual.TotalStarPowerBarsFilled),
                    Description = "Star power bars filled calculation with potential cumulative error",
                    Severity = InconsistencySeverity.Medium
                });
            }
        }

        /// <summary>
        /// Validates overall state consistency rules.
        /// </summary>
        private void ValidateStateConsistency(BaseStats expected, BaseStats actual, double tolerance, List<InconsistencyDetails> issues)
        {
            // Validate that notes hit doesn't exceed total notes
            if (actual.NotesHit > actual.TotalNotes)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "NotesHit",
                    ExpectedValue = $"<= {actual.TotalNotes}",
                    ActualValue = actual.NotesHit,
                    Description = "Notes hit cannot exceed total notes",
                    Severity = InconsistencySeverity.Critical
                });
            }

            // Validate that star power phrases hit doesn't exceed total
            if (actual.StarPowerPhrasesHit > actual.TotalStarPowerPhrases)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "StarPowerPhrasesHit",
                    ExpectedValue = $"<= {actual.TotalStarPowerPhrases}",
                    ActualValue = actual.StarPowerPhrasesHit,
                    Description = "Star power phrases hit cannot exceed total phrases",
                    Severity = InconsistencySeverity.Critical
                });
            }

            // Validate score multiplier bounds
            if (actual.ScoreMultiplier < 1 || actual.ScoreMultiplier > 8) // Typical multiplier range
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "ScoreMultiplier",
                    ExpectedValue = "1-8",
                    ActualValue = actual.ScoreMultiplier,
                    Description = "Score multiplier outside expected range",
                    Severity = InconsistencySeverity.Medium
                });
            }
        }

        /// <summary>
        /// Detects potential state corruption.
        /// </summary>
        private void DetectStateCorruption(BaseStats expected, BaseStats actual, List<InconsistencyDetails> issues)
        {
            // Check for negative values where they shouldn't occur
            var nonNegativeProperties = new[]
            {
                ("NotesHit", actual.NotesHit),
                ("TotalNotes", actual.TotalNotes),
                ("Combo", actual.Combo),
                ("MaxCombo", actual.MaxCombo),
                ("StarPowerPhrasesHit", actual.StarPowerPhrasesHit),
                ("TotalStarPowerPhrases", actual.TotalStarPowerPhrases)
            };

            foreach (var (propertyName, value) in nonNegativeProperties)
            {
                if (value < 0)
                {
                    issues.Add(new InconsistencyDetails
                    {
                        PropertyName = propertyName,
                        ExpectedValue = ">= 0",
                        ActualValue = value,
                        Description = $"{propertyName} should not be negative",
                        Severity = InconsistencySeverity.Critical
                    });
                }
            }

            // Check for NaN or infinity in floating-point values
            var floatingPointProperties = new[]
            {
                ("Stars", actual.Stars),
                ("Percent", actual.Percent),
                ("TimeInStarPower", actual.TimeInStarPower),
                ("TotalStarPowerBarsFilled", actual.TotalStarPowerBarsFilled)
            };

            foreach (var (propertyName, value) in floatingPointProperties)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    issues.Add(new InconsistencyDetails
                    {
                        PropertyName = propertyName,
                        ExpectedValue = "finite number",
                        ActualValue = value,
                        Description = $"{propertyName} contains NaN or infinity",
                        Severity = InconsistencySeverity.Critical
                    });
                }
            }
        }

        /// <summary>
        /// Validates state transitions between consecutive states.
        /// </summary>
        private void ValidateStateTransition(BaseStats previous, BaseStats current, double tolerance, List<InconsistencyDetails> issues, int stateIndex)
        {
            // Validate that certain values only increase
            ValidateMonotonicIncrease(issues, "NotesHit", previous.NotesHit, current.NotesHit, stateIndex);
            ValidateMonotonicIncrease(issues, "StarPowerPhrasesHit", previous.StarPowerPhrasesHit, current.StarPowerPhrasesHit, stateIndex);
            ValidateMonotonicIncrease(issues, "TotalStarPowerTicks", previous.TotalStarPowerTicks, current.TotalStarPowerTicks, stateIndex);
            ValidateMonotonicIncrease(issues, "StarPowerWhammyTicks", previous.StarPowerWhammyTicks, current.StarPowerWhammyTicks, stateIndex);

            // Validate that MaxCombo never decreases
            if (current.MaxCombo < previous.MaxCombo)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = "MaxCombo",
                    ExpectedValue = $">= {previous.MaxCombo}",
                    ActualValue = current.MaxCombo,
                    Description = $"MaxCombo decreased between states {stateIndex - 1} and {stateIndex}",
                    Severity = InconsistencySeverity.High
                });
            }
        }

        /// <summary>
        /// Validates that a property only increases monotonically.
        /// </summary>
        private void ValidateMonotonicIncrease<T>(List<InconsistencyDetails> issues, string propertyName, T previous, T current, int stateIndex) where T : IComparable<T>
        {
            if (current.CompareTo(previous) < 0)
            {
                issues.Add(new InconsistencyDetails
                {
                    PropertyName = propertyName,
                    ExpectedValue = $">= {previous}",
                    ActualValue = current,
                    Description = $"{propertyName} decreased between states {stateIndex - 1} and {stateIndex}",
                    Severity = InconsistencySeverity.High
                });
            }
        }

        /// <summary>
        /// Validates a single property with appropriate comparison logic.
        /// </summary>
        private void ValidateProperty<T>(List<InconsistencyDetails> issues, string propertyName, T expected, T actual, double tolerance)
        {
            if (!_floatingPointComparer.AreEqual(expected!, actual!, tolerance))
            {
                var difference = 0.0;
                var relativeError = 0.0;

                if (expected is IConvertible && actual is IConvertible)
                {
                    var expectedDouble = Convert.ToDouble(expected);
                    var actualDouble = Convert.ToDouble(actual);
                    difference = actualDouble - expectedDouble;
                    relativeError = _floatingPointComparer.GetRelativeError(expectedDouble, actualDouble);
                }

                issues.Add(new InconsistencyDetails
                {
                    PropertyName = propertyName,
                    ExpectedValue = expected,
                    ActualValue = actual,
                    NumericalDifference = difference,
                    RelativeError = relativeError,
                    Description = $"{propertyName} value mismatch",
                    Severity = DetermineSeverity(propertyName, difference)
                });
            }
        }

        /// <summary>
        /// Determines the severity of an inconsistency based on property name and difference.
        /// </summary>
        private InconsistencySeverity DetermineSeverity(string propertyName, double difference)
        {
            // Critical properties that should never differ
            var criticalProperties = new[] { "TotalNotes", "TotalStarPowerPhrases" };
            if (criticalProperties.Contains(propertyName))
                return InconsistencySeverity.Critical;

            // High importance properties
            var highImportanceProperties = new[] { "TotalScore", "NotesHit", "MaxCombo" };
            if (highImportanceProperties.Contains(propertyName))
                return Math.Abs(difference) > 1 ? InconsistencySeverity.High : InconsistencySeverity.Medium;

            // Default severity based on magnitude
            return Math.Abs(difference) switch
            {
                > 100 => InconsistencySeverity.High,
                > 10 => InconsistencySeverity.Medium,
                _ => InconsistencySeverity.Low
            };
        }

        /// <summary>
        /// Initializes default validation rules.
        /// </summary>
        private void InitializeDefaultValidationRules()
        {
            // Add custom validation rules as needed
            _validationRules["ScoreConsistency"] = new ValidationRule
            {
                Name = "ScoreConsistency",
                Description = "Total score should equal sum of component scores",
                Validator = (expected, actual) => 
                {
                    var expectedTotal = expected.CommittedScore + expected.PendingScore + expected.SoloBonuses;
                    return expectedTotal == expected.TotalScore;
                }
            };
        }
    }

    /// <summary>
    /// Represents a validation rule for engine states.
    /// </summary>
    public class ValidationRule
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Func<BaseStats, BaseStats, bool> Validator { get; set; } = (_, _) => true;
    }
}