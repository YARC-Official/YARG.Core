using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YARG.Core.Engine;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Fuzzing.Models;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Default implementation of IConsistencyValidator for validating engine state consistency.
    /// </summary>
    public class DefaultConsistencyValidator : IConsistencyValidator
    {
        private readonly double _defaultTolerance;

        /// <summary>
        /// Initializes a new instance of DefaultConsistencyValidator.
        /// </summary>
        /// <param name="defaultTolerance">Default tolerance for floating-point comparisons</param>
        public DefaultConsistencyValidator(double defaultTolerance = 1e-10)
        {
            _defaultTolerance = defaultTolerance;
        }

        /// <summary>
        /// Validates consistency across multiple engine states.
        /// </summary>
        /// <param name="engineStates">Array of engine states to compare</param>
        /// <param name="tolerance">Tolerance for floating-point comparisons</param>
        /// <returns>Consistency validation result</returns>
        public ConsistencyResult ValidateEngineStates(BaseStats[] engineStates, double tolerance)
        {
            if (engineStates == null || engineStates.Length < 2)
            {
                return new ConsistencyResult
                {
                    IsConsistent = true,
                    Inconsistencies = Array.Empty<InconsistencyDetails>(),
                    MaxDeviation = 0.0
                };
            }

            var inconsistencies = new List<InconsistencyDetails>();
            double maxDeviation = 0.0;

            // Use the first state as the reference
            var referenceState = engineStates[0];

            // Compare all other states against the reference
            for (int i = 1; i < engineStates.Length; i++)
            {
                var currentState = engineStates[i];
                var stateInconsistencies = CompareEngineStates(referenceState, currentState, tolerance);
                
                inconsistencies.AddRange(stateInconsistencies);

                // Update max deviation
                foreach (var inconsistency in stateInconsistencies)
                {
                    maxDeviation = Math.Max(maxDeviation, Math.Abs(inconsistency.NumericalDifference));
                }
            }

            return new ConsistencyResult
            {
                IsConsistent = inconsistencies.Count == 0,
                Inconsistencies = inconsistencies.ToArray(),
                MaxDeviation = maxDeviation
            };
        }

        /// <summary>
        /// Checks if two engine states are equivalent within tolerance.
        /// </summary>
        /// <param name="state1">First engine state</param>
        /// <param name="state2">Second engine state</param>
        /// <param name="tolerance">Tolerance for floating-point comparisons</param>
        /// <returns>True if states are equivalent</returns>
        public bool AreStatesEquivalent(BaseStats state1, BaseStats state2, double tolerance)
        {
            if (state1 == null && state2 == null) return true;
            if (state1 == null || state2 == null) return false;

            var inconsistencies = CompareEngineStates(state1, state2, tolerance);
            return inconsistencies.Count == 0;
        }

        /// <summary>
        /// Gets detailed information about inconsistencies between two states.
        /// </summary>
        /// <param name="expected">Expected engine state</param>
        /// <param name="actual">Actual engine state</param>
        /// <returns>Detailed inconsistency information</returns>
        public InconsistencyDetails GetInconsistencyDetails(BaseStats expected, BaseStats actual)
        {
            var inconsistencies = CompareEngineStates(expected, actual, _defaultTolerance);
            
            if (inconsistencies.Count == 0)
            {
                return new InconsistencyDetails
                {
                    PropertyName = "None",
                    Description = "No inconsistencies found",
                    Severity = InconsistencySeverity.Low
                };
            }

            // Return the most significant inconsistency
            return inconsistencies.OrderByDescending(i => Math.Abs(i.NumericalDifference)).First();
        }

        /// <summary>
        /// Compares two engine states and returns a list of inconsistencies.
        /// </summary>
        private List<InconsistencyDetails> CompareEngineStates(BaseStats expected, BaseStats actual, double tolerance)
        {
            var inconsistencies = new List<InconsistencyDetails>();

            if (expected == null || actual == null)
            {
                inconsistencies.Add(new InconsistencyDetails
                {
                    PropertyName = "State",
                    ExpectedValue = expected,
                    ActualValue = actual,
                    Description = "One of the states is null",
                    Severity = InconsistencySeverity.Critical
                });
                return inconsistencies;
            }

            // Compare common BaseStats fields
            CompareProperty(inconsistencies, "TotalScore", expected.TotalScore, actual.TotalScore, tolerance);
            CompareProperty(inconsistencies, "CommittedScore", expected.CommittedScore, actual.CommittedScore, tolerance);
            CompareProperty(inconsistencies, "PendingScore", expected.PendingScore, actual.PendingScore, tolerance);
            CompareProperty(inconsistencies, "Stars", expected.Stars, actual.Stars, tolerance);
            CompareProperty(inconsistencies, "NotesHit", expected.NotesHit, actual.NotesHit, tolerance);
            CompareProperty(inconsistencies, "NotesMissed", expected.NotesMissed, actual.NotesMissed, tolerance);
            CompareProperty(inconsistencies, "Combo", expected.Combo, actual.Combo, tolerance);
            CompareProperty(inconsistencies, "MaxCombo", expected.MaxCombo, actual.MaxCombo, tolerance);
            CompareProperty(inconsistencies, "StarPowerTickAmount", expected.StarPowerTickAmount, actual.StarPowerTickAmount, tolerance);
            CompareProperty(inconsistencies, "StarPowerWhammyTicks", expected.StarPowerWhammyTicks, actual.StarPowerWhammyTicks, tolerance);
            CompareProperty(inconsistencies, "IsStarPowerActive", expected.IsStarPowerActive, actual.IsStarPowerActive, tolerance);

            // Use reflection to compare additional properties specific to derived types
            CompareAdditionalProperties(inconsistencies, expected, actual, tolerance);

            return inconsistencies;
        }

        /// <summary>
        /// Compares additional properties using reflection for derived BaseStats types.
        /// </summary>
        private void CompareAdditionalProperties(List<InconsistencyDetails> inconsistencies, BaseStats expected, BaseStats actual, double tolerance)
        {
            var expectedType = expected.GetType();
            var actualType = actual.GetType();

            if (expectedType != actualType)
            {
                inconsistencies.Add(new InconsistencyDetails
                {
                    PropertyName = "Type",
                    ExpectedValue = expectedType.Name,
                    ActualValue = actualType.Name,
                    Description = "Engine state types do not match",
                    Severity = InconsistencySeverity.Critical
                });
                return;
            }

            // Get all public fields and properties
            var fields = expectedType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !IsBaseStatsProperty(f.Name));
            
            var properties = expectedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !IsBaseStatsProperty(p.Name));

            foreach (var field in fields)
            {
                try
                {
                    var expectedValue = field.GetValue(expected);
                    var actualValue = field.GetValue(actual);

                    ComparePropertyValue(inconsistencies, field.Name, expectedValue, actualValue, tolerance);
                }
                catch (Exception ex)
                {
                    inconsistencies.Add(new InconsistencyDetails
                    {
                        PropertyName = field.Name,
                        Description = $"Error comparing field: {ex.Message}",
                        Severity = InconsistencySeverity.Medium
                    });
                }
            }

            foreach (var property in properties)
            {
                try
                {
                    var expectedValue = property.GetValue(expected);
                    var actualValue = property.GetValue(actual);

                    ComparePropertyValue(inconsistencies, property.Name, expectedValue, actualValue, tolerance);
                }
                catch (Exception ex)
                {
                    inconsistencies.Add(new InconsistencyDetails
                    {
                        PropertyName = property.Name,
                        Description = $"Error comparing property: {ex.Message}",
                        Severity = InconsistencySeverity.Medium
                    });
                }
            }
        }

        /// <summary>
        /// Checks if a property name belongs to the base BaseStats class.
        /// </summary>
        private bool IsBaseStatsProperty(string propertyName)
        {
            var baseProperties = new[] { 
                "TotalScore", "CommittedScore", "PendingScore", "Stars", "NotesHit", "NotesMissed", 
                "Combo", "MaxCombo", "StarPowerTickAmount", "StarPowerWhammyTicks", "IsStarPowerActive",
                "NoteScore", "SustainScore", "MultiplierScore", "ScoreMultiplier", "TotalNotes",
                "TotalStarPowerTicks", "TotalStarPowerBarsFilled", "StarPowerActivationCount",
                "TimeInStarPower", "StarPowerPhrasesHit", "TotalStarPowerPhrases", "StarPowerPhrasesMissed",
                "SoloBonuses", "StarPowerScore", "Percent", "ComboInBandUnits", "BandComboUnits", "StarScore"
            };
            return baseProperties.Contains(propertyName);
        }

        /// <summary>
        /// Compares a specific property and adds inconsistencies if found.
        /// </summary>
        private void CompareProperty<T>(List<InconsistencyDetails> inconsistencies, string propertyName, T expected, T actual, double tolerance)
        {
            ComparePropertyValue(inconsistencies, propertyName, expected, actual, tolerance);
        }

        /// <summary>
        /// Compares property values and adds inconsistencies if found.
        /// </summary>
        private void ComparePropertyValue(List<InconsistencyDetails> inconsistencies, string propertyName, object? expected, object? actual, double tolerance)
        {
            if (expected == null && actual == null) return;

            if (expected == null || actual == null)
            {
                inconsistencies.Add(new InconsistencyDetails
                {
                    PropertyName = propertyName,
                    ExpectedValue = expected,
                    ActualValue = actual,
                    Description = $"Property {propertyName}: one value is null",
                    Severity = InconsistencySeverity.High
                });
                return;
            }

            // Handle numeric types with tolerance
            if (IsNumericType(expected.GetType()))
            {
                double expectedDouble = Convert.ToDouble(expected);
                double actualDouble = Convert.ToDouble(actual);
                double difference = Math.Abs(expectedDouble - actualDouble);

                if (difference > tolerance)
                {
                    double relativeError = expectedDouble != 0 ? (difference / Math.Abs(expectedDouble)) * 100 : 0;

                    inconsistencies.Add(new InconsistencyDetails
                    {
                        PropertyName = propertyName,
                        ExpectedValue = expected,
                        ActualValue = actual,
                        NumericalDifference = actualDouble - expectedDouble,
                        RelativeError = relativeError,
                        Description = $"Property {propertyName}: values differ by {difference:E6}",
                        Severity = DetermineSeverity(difference, tolerance)
                    });
                }
            }
            else
            {
                // Handle non-numeric types with equality comparison
                if (!expected.Equals(actual))
                {
                    inconsistencies.Add(new InconsistencyDetails
                    {
                        PropertyName = propertyName,
                        ExpectedValue = expected,
                        ActualValue = actual,
                        Description = $"Property {propertyName}: values are not equal",
                        Severity = InconsistencySeverity.Medium
                    });
                }
            }
        }

        /// <summary>
        /// Determines if a type is numeric.
        /// </summary>
        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(byte) || type == typeof(sbyte);
        }

        /// <summary>
        /// Determines the severity of an inconsistency based on the difference and tolerance.
        /// </summary>
        private InconsistencySeverity DetermineSeverity(double difference, double tolerance)
        {
            double ratio = difference / tolerance;

            return ratio switch
            {
                <= 10 => InconsistencySeverity.Low,
                <= 100 => InconsistencySeverity.Medium,
                <= 1000 => InconsistencySeverity.High,
                _ => InconsistencySeverity.Critical
            };
        }
    }
}