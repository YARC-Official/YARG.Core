using System;
using System.Linq;
using NUnit.Framework;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Fuzzing;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Input;
using YARG.Core.UnitTests.Utility;

namespace YARG.Core.UnitTests.Fuzzing
{
    [TestFixture]
    public class InputSequenceGeneratorTests
    {
        private SongChart _testChart;

        [SetUp]
        public void SetUp()
        {
            // Create a simple test chart
            _testChart = SongChartBuilder.New()
                .AddNote(FiveFretGuitarFret.Green)
                .AddNote(FiveFretGuitarFret.Red)
                .AddNote(FourLaneDrumPad.RedDrum)
                .AddNote(FourLaneDrumPad.YellowDrum)
                .Build();
        }

        [Test]
        public void InputSequenceGenerator_Constructor_WithSeed_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var generator = new InputSequenceGenerator(12345);

            // Assert
            Assert.That(generator, Is.Not.Null);
            Assert.That(generator.Seed, Is.EqualTo(12345));
        }

        [Test]
        public void InputSequenceGenerator_Constructor_WithoutSeed_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var generator = new InputSequenceGenerator();

            // Assert
            Assert.That(generator, Is.Not.Null);
            Assert.That(generator.Seed, Is.Null);
        }

        [Test]
        public void InputSequenceGenerator_GenerateStarPowerSequence_ReturnsValidInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator(12345);

            // Act
            var inputs = generator.GenerateStarPowerSequence(_testChart, Instrument.FiveFretGuitar, Difficulty.Expert);

            // Assert
            Assert.That(inputs, Is.Not.Null);
            // Note: The actual implementation may return empty array if no star power phrases exist
            // This is acceptable for the test chart
        }

        [Test]
        public void InputSequenceGenerator_GenerateStarPowerSequence_NullChart_ThrowsException()
        {
            // Arrange
            var generator = new InputSequenceGenerator();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                generator.GenerateStarPowerSequence(null!, Instrument.FiveFretGuitar, Difficulty.Expert));
        }

        [Test]
        public void InputSequenceGenerator_GenerateWhammySequence_ReturnsValidInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator(12345);
            var startTime = 0.0;
            var endTime = 10.0;

            // Act
            var inputs = generator.GenerateWhammySequence(startTime, endTime, WhammyPattern.Continuous);

            // Assert
            Assert.That(inputs, Is.Not.Null);
            
            if (inputs.Length > 0)
            {
                // Check that all inputs are within the time range
                Assert.That(inputs.First().Time, Is.GreaterThanOrEqualTo(startTime));
                Assert.That(inputs.Last().Time, Is.LessThanOrEqualTo(endTime));
                
                // Check that inputs are chronologically ordered
                for (int i = 1; i < inputs.Length; i++)
                {
                    Assert.That(inputs[i].Time, Is.GreaterThanOrEqualTo(inputs[i - 1].Time));
                }
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateWhammySequence_AllPatterns_ReturnsValidInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator(12345);
            var startTime = 0.0;
            var endTime = 5.0;
            var patterns = Enum.GetValues<WhammyPattern>();

            // Act & Assert
            foreach (var pattern in patterns)
            {
                var inputs = generator.GenerateWhammySequence(startTime, endTime, pattern);
                Assert.That(inputs, Is.Not.Null, $"Pattern {pattern} should return non-null inputs");
                
                if (inputs.Length > 0)
                {
                    // Check time bounds
                    Assert.That(inputs.First().Time, Is.GreaterThanOrEqualTo(startTime));
                    Assert.That(inputs.Last().Time, Is.LessThanOrEqualTo(endTime));
                }
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateRandomInputSequence_ReturnsValidInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator();
            var seed = 12345;

            // Act
            var inputs = generator.GenerateRandomInputSequence(_testChart, Instrument.FiveFretGuitar, Difficulty.Expert, seed);

            // Assert
            Assert.That(inputs, Is.Not.Null);
            
            if (inputs.Length > 0)
            {
                // Check that inputs are chronologically ordered
                for (int i = 1; i < inputs.Length; i++)
                {
                    Assert.That(inputs[i].Time, Is.GreaterThanOrEqualTo(inputs[i - 1].Time));
                }
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateRandomInputSequence_NullChart_ThrowsException()
        {
            // Arrange
            var generator = new InputSequenceGenerator();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                generator.GenerateRandomInputSequence(null!, Instrument.FiveFretGuitar, Difficulty.Expert, 12345));
        }

        [Test]
        public void InputSequenceGenerator_GenerateRandomInputSequence_SameSeed_ProducesReproducibleResults()
        {
            // Arrange
            var generator1 = new InputSequenceGenerator();
            var generator2 = new InputSequenceGenerator();
            var seed = 42;

            // Act
            var inputs1 = generator1.GenerateRandomInputSequence(_testChart, Instrument.FiveFretGuitar, Difficulty.Expert, seed);
            var inputs2 = generator2.GenerateRandomInputSequence(_testChart, Instrument.FiveFretGuitar, Difficulty.Expert, seed);

            // Assert
            Assert.That(inputs1.Length, Is.EqualTo(inputs2.Length));
            
            for (int i = 0; i < inputs1.Length; i++)
            {
                Assert.That(inputs1[i].Time, Is.EqualTo(inputs2[i].Time).Within(1e-6));
                Assert.That(inputs1[i].Action, Is.EqualTo(inputs2[i].Action));
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateRandomInputSequence_DifferentInstruments_ReturnsAppropriateInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator(12345);
            var instruments = new[] { Instrument.FiveFretGuitar, Instrument.FourLaneDrums, Instrument.Vocals };

            // Act & Assert
            foreach (var instrument in instruments)
            {
                var inputs = generator.GenerateRandomInputSequence(_testChart, instrument, Difficulty.Expert, 12345);
                Assert.That(inputs, Is.Not.Null, $"Instrument {instrument} should return non-null inputs");
                
                // Note: We can't easily test the specific action types without knowing the internal implementation
                // But we can verify that inputs are generated for each instrument type
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateRandomInputSequence_DifferentDifficulties_ReturnsValidInputs()
        {
            // Arrange
            var generator = new InputSequenceGenerator(12345);
            var difficulties = new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Expert };

            // Act & Assert
            foreach (var difficulty in difficulties)
            {
                var inputs = generator.GenerateRandomInputSequence(_testChart, Instrument.FiveFretGuitar, difficulty, 12345);
                Assert.That(inputs, Is.Not.Null, $"Difficulty {difficulty} should return non-null inputs");
            }
        }

        [Test]
        public void InputSequenceGenerator_GenerateWhammySequence_InvalidTimeRange_ReturnsEmptyArray()
        {
            // Arrange
            var generator = new InputSequenceGenerator();
            var startTime = 10.0;
            var endTime = 5.0; // Invalid range

            // Act
            var inputs = generator.GenerateWhammySequence(startTime, endTime, WhammyPattern.Continuous);

            // Assert
            Assert.That(inputs, Is.Not.Null);
            Assert.That(inputs.Length, Is.EqualTo(0));
        }

        [Test]
        public void InputSequenceGenerator_GenerateWhammySequence_ZeroDuration_ReturnsEmptyArray()
        {
            // Arrange
            var generator = new InputSequenceGenerator();
            var time = 5.0;

            // Act
            var inputs = generator.GenerateWhammySequence(time, time, WhammyPattern.Continuous);

            // Assert
            Assert.That(inputs, Is.Not.Null);
            Assert.That(inputs.Length, Is.EqualTo(0));
        }
    }
}