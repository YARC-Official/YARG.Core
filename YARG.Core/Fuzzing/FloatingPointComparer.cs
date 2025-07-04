using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Provides sophisticated floating-point comparison logic with configurable tolerance.
    /// </summary>
    public class FloatingPointComparer
    {
        private readonly double _absoluteTolerance;
        private readonly double _relativeTolerance;
        private readonly Dictionary<Type, double> _typeSpecificTolerances;

        /// <summary>
        /// Initializes a new instance of FloatingPointComparer.
        /// </summary>
        /// <param name="absoluteTolerance">Absolute tolerance for comparisons</param>
        /// <param name="relativeTolerance">Relative tolerance as a percentage (0.0 to 1.0)</param>
        public FloatingPointComparer(double absoluteTolerance = 1e-10, double relativeTolerance = 1e-6)
        {
            _absoluteTolerance = absoluteTolerance;
            _relativeTolerance = relativeTolerance;
            _typeSpecificTolerances = new Dictionary<Type, double>();
            
            // Set default type-specific tolerances
            SetTypeSpecificTolerance<float>(1e-6f);
            SetTypeSpecificTolerance<double>(1e-10);
            SetTypeSpecificTolerance<decimal>(1e-12);
        }

        /// <summary>
        /// Sets a specific tolerance for a given numeric type.
        /// </summary>
        /// <typeparam name="T">Numeric type</typeparam>
        /// <param name="tolerance">Tolerance value for this type</param>
        public void SetTypeSpecificTolerance<T>(double tolerance) where T : struct
        {
            _typeSpecificTolerances[typeof(T)] = tolerance;
        }

        /// <summary>
        /// Compares two floating-point values for equality within tolerance.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="customTolerance">Custom tolerance (optional)</param>
        /// <returns>True if values are equal within tolerance</returns>
        public bool AreEqual(double expected, double actual, double? customTolerance = null)
        {
            var tolerance = customTolerance ?? GetEffectiveTolerance(expected, typeof(double));
            return AreEqualWithTolerance(expected, actual, tolerance);
        }

        /// <summary>
        /// Compares two floating-point values for equality within tolerance.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="customTolerance">Custom tolerance (optional)</param>
        /// <returns>True if values are equal within tolerance</returns>
        public bool AreEqual(float expected, float actual, float? customTolerance = null)
        {
            var tolerance = customTolerance ?? (float)GetEffectiveTolerance(expected, typeof(float));
            return AreEqualWithTolerance(expected, actual, tolerance);
        }

        /// <summary>
        /// Compares two decimal values for equality within tolerance.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="customTolerance">Custom tolerance (optional)</param>
        /// <returns>True if values are equal within tolerance</returns>
        public bool AreEqual(decimal expected, decimal actual, decimal? customTolerance = null)
        {
            var tolerance = customTolerance ?? (decimal)GetEffectiveTolerance((double)expected, typeof(decimal));
            return Math.Abs(expected - actual) <= tolerance;
        }

        /// <summary>
        /// Compares two numeric values of any type for equality within tolerance.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="customTolerance">Custom tolerance (optional)</param>
        /// <returns>True if values are equal within tolerance</returns>
        public bool AreEqual(object expected, object actual, double? customTolerance = null)
        {
            if (expected == null && actual == null) return true;
            if (expected == null || actual == null) return false;

            var expectedType = expected.GetType();
            var actualType = actual.GetType();

            if (expectedType != actualType) return false;

            if (!IsNumericType(expectedType)) 
                return expected.Equals(actual);

            double expectedDouble = Convert.ToDouble(expected);
            double actualDouble = Convert.ToDouble(actual);
            var tolerance = customTolerance ?? GetEffectiveTolerance(expectedDouble, expectedType);

            return AreEqualWithTolerance(expectedDouble, actualDouble, tolerance);
        }

        /// <summary>
        /// Calculates the absolute difference between two values.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <returns>Absolute difference</returns>
        public double GetAbsoluteDifference(double expected, double actual)
        {
            return Math.Abs(actual - expected);
        }

        /// <summary>
        /// Calculates the relative error between two values as a percentage.
        /// </summary>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <returns>Relative error as a percentage (0.0 to 100.0)</returns>
        public double GetRelativeError(double expected, double actual)
        {
            if (Math.Abs(expected) < double.Epsilon)
            {
                return Math.Abs(actual) < double.Epsilon ? 0.0 : double.PositiveInfinity;
            }

            return Math.Abs((actual - expected) / expected) * 100.0;
        }

        /// <summary>
        /// Detects cumulative floating-point errors in a sequence of values.
        /// </summary>
        /// <param name="values">Sequence of values to analyze</param>
        /// <param name="expectedPattern">Expected pattern (e.g., arithmetic progression)</param>
        /// <returns>Cumulative error analysis result</returns>
        public CumulativeErrorAnalysis AnalyzeCumulativeError(double[] values, Func<int, double> expectedPattern)
        {
            if (values == null || values.Length == 0)
                return new CumulativeErrorAnalysis { IsValid = false };

            var errors = new double[values.Length];
            double maxError = 0.0;
            double totalError = 0.0;
            int significantErrorCount = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double expected = expectedPattern(i);
                double error = Math.Abs(values[i] - expected);
                errors[i] = error;
                
                maxError = Math.Max(maxError, error);
                totalError += error;

                if (error > _absoluteTolerance * 10) // Significant error threshold
                {
                    significantErrorCount++;
                }
            }

            return new CumulativeErrorAnalysis
            {
                IsValid = true,
                Errors = errors,
                MaxError = maxError,
                AverageError = totalError / values.Length,
                SignificantErrorCount = significantErrorCount,
                HasSystematicDrift = DetectSystematicDrift(errors),
                DriftRate = CalculateDriftRate(errors)
            };
        }

        /// <summary>
        /// Specialized comparison for star power calculations that may accumulate errors.
        /// </summary>
        /// <param name="expectedStarPower">Expected star power amount</param>
        /// <param name="actualStarPower">Actual star power amount</param>
        /// <param name="calculationSteps">Number of calculation steps that led to this value</param>
        /// <returns>True if values are equal considering cumulative error</returns>
        public bool AreStarPowerValuesEqual(double expectedStarPower, double actualStarPower, int calculationSteps)
        {
            // Increase tolerance based on number of calculation steps to account for cumulative error
            double cumulativeTolerance = _absoluteTolerance * Math.Sqrt(calculationSteps);
            return AreEqualWithTolerance(expectedStarPower, actualStarPower, cumulativeTolerance);
        }

        /// <summary>
        /// Core floating-point comparison logic with tolerance.
        /// </summary>
        private bool AreEqualWithTolerance(double expected, double actual, double tolerance)
        {
            // Handle special cases
            if (double.IsNaN(expected) && double.IsNaN(actual)) return true;
            if (double.IsNaN(expected) || double.IsNaN(actual)) return false;
            if (double.IsInfinity(expected) && double.IsInfinity(actual))
                return Math.Sign(expected) == Math.Sign(actual);
            if (double.IsInfinity(expected) || double.IsInfinity(actual)) return false;

            double absoluteDifference = Math.Abs(actual - expected);
            
            // Absolute tolerance check
            if (absoluteDifference <= tolerance) return true;

            // Relative tolerance check for larger values
            if (Math.Abs(expected) > tolerance)
            {
                double relativeDifference = absoluteDifference / Math.Abs(expected);
                return relativeDifference <= _relativeTolerance;
            }

            return false;
        }

        /// <summary>
        /// Gets the effective tolerance for a value and type.
        /// </summary>
        private double GetEffectiveTolerance(double value, Type type)
        {
            if (_typeSpecificTolerances.TryGetValue(type, out double typeSpecificTolerance))
            {
                return typeSpecificTolerance;
            }

            return _absoluteTolerance;
        }

        /// <summary>
        /// Checks if a type is numeric.
        /// </summary>
        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(byte) || type == typeof(sbyte);
        }

        /// <summary>
        /// Detects systematic drift in error patterns.
        /// </summary>
        private bool DetectSystematicDrift(double[] errors)
        {
            if (errors.Length < 3) return false;

            // Check if errors are consistently increasing or decreasing
            int increasingCount = 0;
            int decreasingCount = 0;

            for (int i = 1; i < errors.Length; i++)
            {
                if (errors[i] > errors[i - 1]) increasingCount++;
                else if (errors[i] < errors[i - 1]) decreasingCount++;
            }

            // Consider it systematic drift if 70% of changes are in the same direction
            double threshold = errors.Length * 0.7;
            return increasingCount > threshold || decreasingCount > threshold;
        }

        /// <summary>
        /// Calculates the rate of drift in errors.
        /// </summary>
        private double CalculateDriftRate(double[] errors)
        {
            if (errors.Length < 2) return 0.0;

            // Simple linear regression to find drift rate
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = errors.Length;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += errors[i];
                sumXY += i * errors[i];
                sumX2 += i * i;
            }

            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < double.Epsilon) return 0.0;

            return (n * sumXY - sumX * sumY) / denominator;
        }
    }

    /// <summary>
    /// Result of cumulative error analysis.
    /// </summary>
    public class CumulativeErrorAnalysis
    {
        /// <summary>Whether the analysis is valid</summary>
        public bool IsValid { get; set; }

        /// <summary>Individual errors for each value</summary>
        public double[] Errors { get; set; } = Array.Empty<double>();

        /// <summary>Maximum error found</summary>
        public double MaxError { get; set; }

        /// <summary>Average error across all values</summary>
        public double AverageError { get; set; }

        /// <summary>Number of errors above significance threshold</summary>
        public int SignificantErrorCount { get; set; }

        /// <summary>Whether systematic drift was detected</summary>
        public bool HasSystematicDrift { get; set; }

        /// <summary>Rate of drift (error increase per step)</summary>
        public double DriftRate { get; set; }
    }
}