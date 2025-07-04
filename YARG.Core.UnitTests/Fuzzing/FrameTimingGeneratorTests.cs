using System;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Fuzzing;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Fuzzing.FrameTimingGenerators;

namespace YARG.Core.UnitTests.Fuzzing
{
    [TestFixture]
    public class FrameTimingGeneratorTests
    {
        private const double StartTime = 0.0;
        private const double EndTime = 10.0;
        private const double MinFrameTime = 0.008; // ~120 FPS
        private const double MaxFrameTime = 0.033; // ~30 FPS
        private const double Tolerance = 1e-6;

        [Test]
        public void DefaultFrameTimingGenerator_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var generator = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Assert
            Assert.That(generator, Is.Not.Null);
        }

        [Test]
        public void DefaultFrameTimingGenerator_GetAvailablePatterns_ReturnsAllPatterns()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator();

            // Act
            var patterns = generator.GetAvailablePatterns();

            // Assert
            Assert.That(patterns, Is.Not.Null);
            Assert.That(patterns.Length, Is.EqualTo(5));
            Assert.That(patterns, Contains.Item(FrameTimingPattern.Regular));
            Assert.That(patterns, Contains.Item(FrameTimingPattern.Irregular));
            Assert.That(patterns, Contains.Item(FrameTimingPattern.MicroStutters));
            Assert.That(patterns, Contains.Item(FrameTimingPattern.VariableFrameRate));
            Assert.That(patterns, Contains.Item(FrameTimingPattern.SubFramePrecision));
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_Regular_ReturnsConsistentTimes()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator();

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.Regular);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are within the expected range
            Assert.That(frameTimes.First(), Is.EqualTo(StartTime).Within(Tolerance));
            Assert.That(frameTimes.Last(), Is.LessThanOrEqualTo(EndTime));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_Irregular_ReturnsVariedTimes()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.Irregular);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are within the expected range
            Assert.That(frameTimes.First(), Is.EqualTo(StartTime).Within(Tolerance));
            Assert.That(frameTimes.Last(), Is.LessThanOrEqualTo(EndTime));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
            
            // Check that frame deltas vary (not all the same)
            var deltas = new double[frameTimes.Length - 1];
            for (int i = 1; i < frameTimes.Length; i++)
            {
                deltas[i - 1] = frameTimes[i] - frameTimes[i - 1];
            }
            
            var uniqueDeltas = deltas.Distinct().Count();
            Assert.That(uniqueDeltas, Is.GreaterThan(1), "Frame deltas should vary for irregular pattern");
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_MicroStutters_ContainsSpikes()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.MicroStutters);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are within the expected range
            Assert.That(frameTimes.First(), Is.EqualTo(StartTime).Within(Tolerance));
            Assert.That(frameTimes.Last(), Is.LessThanOrEqualTo(EndTime));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_VariableFrameRate_ReturnsValidTimes()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.VariableFrameRate);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are within the expected range
            Assert.That(frameTimes.First(), Is.EqualTo(StartTime).Within(Tolerance));
            Assert.That(frameTimes.Last(), Is.LessThanOrEqualTo(EndTime));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_SubFramePrecision_ReturnsHighPrecisionTimes()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.SubFramePrecision);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are within the expected range
            Assert.That(frameTimes.First(), Is.EqualTo(StartTime).Within(Tolerance));
            Assert.That(frameTimes.Last(), Is.LessThanOrEqualTo(EndTime));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_InvalidTimeRange_ReturnsEmptyArray()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator();

            // Act
            var frameTimes = generator.GenerateFrameTimes(EndTime, StartTime, FrameTimingPattern.Regular);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.EqualTo(0));
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_UnknownPattern_ThrowsException()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                generator.GenerateFrameTimes(StartTime, EndTime, (FrameTimingPattern)999));
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_SameStartEndTime_ReturnsEmptyArray()
        {
            // Arrange
            var generator = new DefaultFrameTimingGenerator();
            var sameTime = 5.0;

            // Act
            var frameTimes = generator.GenerateFrameTimes(sameTime, sameTime, FrameTimingPattern.Regular);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.EqualTo(0));
        }

        [Test]
        public void DefaultFrameTimingGenerator_GenerateFrameTimes_WithSeed_ProducesReproducibleResults()
        {
            // Arrange
            const int seed = 42;
            var generator1 = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, seed);
            var generator2 = new DefaultFrameTimingGenerator(MinFrameTime, MaxFrameTime, seed);

            // Act
            var frameTimes1 = generator1.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.Irregular);
            var frameTimes2 = generator2.GenerateFrameTimes(StartTime, EndTime, FrameTimingPattern.Irregular);

            // Assert
            Assert.That(frameTimes1.Length, Is.EqualTo(frameTimes2.Length));
            for (int i = 0; i < frameTimes1.Length; i++)
            {
                Assert.That(frameTimes1[i], Is.EqualTo(frameTimes2[i]).Within(Tolerance));
            }
        }

        [Test]
        public void RegularFrameTimingGenerator_GenerateFrameTimes_ReturnsConsistentIntervals()
        {
            // Arrange
            var generator = new RegularFrameTimingGenerator(60.0); // 60 FPS

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame intervals are consistent (approximately 1/60 seconds)
            var expectedInterval = 1.0 / 60.0;
            for (int i = 1; i < frameTimes.Length; i++)
            {
                var actualInterval = frameTimes[i] - frameTimes[i - 1];
                Assert.That(actualInterval, Is.EqualTo(expectedInterval).Within(Tolerance));
            }
        }

        [Test]
        public void IrregularFrameTimingGenerator_GenerateFrameTimes_ReturnsVariedIntervals()
        {
            // Arrange
            var generator = new IrregularFrameTimingGenerator(MinFrameTime, MaxFrameTime, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame intervals vary
            var intervals = new double[frameTimes.Length - 1];
            for (int i = 1; i < frameTimes.Length; i++)
            {
                intervals[i - 1] = frameTimes[i] - frameTimes[i - 1];
                // Each interval should be within the specified bounds
                Assert.That(intervals[i - 1], Is.GreaterThanOrEqualTo(MinFrameTime));
                Assert.That(intervals[i - 1], Is.LessThanOrEqualTo(MaxFrameTime));
            }
            
            // Check that intervals are not all the same
            var uniqueIntervals = intervals.Distinct().Count();
            Assert.That(uniqueIntervals, Is.GreaterThan(1));
        }

        [Test]
        public void MicroStutterFrameTimingGenerator_GenerateFrameTimes_ContainsStutters()
        {
            // Arrange
            var generator = new MicroStutterFrameTimingGenerator(60.0, 0.1, 2.0, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
            
            // Check that there are some larger intervals (stutters)
            var intervals = new double[frameTimes.Length - 1];
            for (int i = 1; i < frameTimes.Length; i++)
            {
                intervals[i - 1] = frameTimes[i] - frameTimes[i - 1];
            }
            
            var baseInterval = 1.0 / 60.0;
            var largeIntervals = intervals.Where(interval => interval > baseInterval * 1.5).Count();
            Assert.That(largeIntervals, Is.GreaterThan(0), "Should contain some micro-stutters");
        }

        [Test]
        public void VariableFrameRateGenerator_GenerateFrameTimes_ReturnsValidFrameRates()
        {
            // Arrange
            var generator = new VariableFrameRateGenerator(30.0, 120.0, 1.0, 12345);

            // Act
            var frameTimes = generator.GenerateFrameTimes(StartTime, EndTime);

            // Assert
            Assert.That(frameTimes, Is.Not.Null);
            Assert.That(frameTimes.Length, Is.GreaterThan(0));
            
            // Check that frame times are monotonically increasing
            for (int i = 1; i < frameTimes.Length; i++)
            {
                Assert.That(frameTimes[i], Is.GreaterThan(frameTimes[i - 1]));
            }
            
            // Check that frame intervals are within expected bounds
            for (int i = 1; i < frameTimes.Length; i++)
            {
                var interval = frameTimes[i] - frameTimes[i - 1];
                Assert.That(interval, Is.GreaterThanOrEqualTo(1.0 / 120.0)); // Max FPS = 120
                Assert.That(interval, Is.LessThanOrEqualTo(1.0 / 30.0));     // Min FPS = 30
            }
        }
    }
}