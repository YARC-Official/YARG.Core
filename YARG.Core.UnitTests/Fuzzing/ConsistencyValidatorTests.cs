using System;
using NUnit.Framework;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Drums;
using YARG.Core.Fuzzing;
using YARG.Core.Fuzzing.Models;

namespace YARG.Core.UnitTests.Fuzzing
{
    [TestFixture]
    public class ConsistencyValidatorTests
    {
        private DefaultConsistencyValidator _validator;
        private const double DefaultTolerance = 1e-10;

        [SetUp]
        public void SetUp()
        {
            _validator = new DefaultConsistencyValidator(DefaultTolerance);
        }

        [Test]
        public void DefaultConsistencyValidator_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var validator = new DefaultConsistencyValidator(1e-6);

            // Assert
            Assert.That(validator, Is.Not.Null);
        }

        [Test]
        public void ValidateEngineStates_NullArray_ReturnsConsistent()
        {
            // Arrange
            BaseStats[]? states = null;

            // Act
            var result = _validator.ValidateEngineStates(states!, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
            Assert.That(result.MaxDeviation, Is.EqualTo(0.0));
        }

        [Test]
        public void ValidateEngineStates_EmptyArray_ReturnsConsistent()
        {
            // Arrange
            var states = new BaseStats[0];

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
            Assert.That(result.MaxDeviation, Is.EqualTo(0.0));
        }

        [Test]
        public void ValidateEngineStates_SingleState_ReturnsConsistent()
        {
            // Arrange
            var states = new BaseStats[] { CreateTestGuitarStats() };

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
            Assert.That(result.MaxDeviation, Is.EqualTo(0.0));
        }

        [Test]
        public void ValidateEngineStates_IdenticalStates_ReturnsConsistent()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            var states = new BaseStats[] { state1, state2 };

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
            Assert.That(result.MaxDeviation, Is.EqualTo(0.0));
        }

        [Test]
        public void ValidateEngineStates_DifferentScores_ReturnsInconsistent()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = 1000; // Different score

            var states = new BaseStats[] { state1, state2 };

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.False);
            Assert.That(result.Inconsistencies.Length, Is.GreaterThan(0));
            Assert.That(result.MaxDeviation, Is.GreaterThan(0));
        }

        [Test]
        public void ValidateEngineStates_SmallDifferenceWithinTolerance_ReturnsConsistent()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = state1.CommittedScore + 1; // Small difference

            var states = new BaseStats[] { state1, state2 };
            var largeTolerance = 2.0; // Use a tolerance larger than the difference

            // Act
            var result = _validator.ValidateEngineStates(states, largeTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
        }

        [Test]
        public void ValidateEngineStates_SmallDifferenceOutsideTolerance_ReturnsInconsistent()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = state1.CommittedScore + 1; // Small but detectable difference

            var states = new BaseStats[] { state1, state2 };
            var smallTolerance = 0.5; // Use a tolerance smaller than the difference

            // Act
            var result = _validator.ValidateEngineStates(states, smallTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.False);
            Assert.That(result.Inconsistencies.Length, Is.GreaterThan(0));
        }

        [Test]
        public void AreStatesEquivalent_BothNull_ReturnsTrue()
        {
            // Act
            var result = _validator.AreStatesEquivalent(null!, null!, DefaultTolerance);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void AreStatesEquivalent_OneNull_ReturnsFalse()
        {
            // Arrange
            var state = CreateTestGuitarStats();

            // Act
            var result1 = _validator.AreStatesEquivalent(state, null!, DefaultTolerance);
            var result2 = _validator.AreStatesEquivalent(null!, state, DefaultTolerance);

            // Assert
            Assert.That(result1, Is.False);
            Assert.That(result2, Is.False);
        }

        [Test]
        public void AreStatesEquivalent_IdenticalStates_ReturnsTrue()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();

            // Act
            var result = _validator.AreStatesEquivalent(state1, state2, DefaultTolerance);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void AreStatesEquivalent_DifferentStates_ReturnsFalse()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = 1000;

            // Act
            var result = _validator.AreStatesEquivalent(state1, state2, DefaultTolerance);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetInconsistencyDetails_IdenticalStates_ReturnsNoInconsistency()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();

            // Act
            var details = _validator.GetInconsistencyDetails(state1, state2);

            // Assert
            Assert.That(details, Is.Not.Null);
            Assert.That(details.PropertyName, Is.EqualTo("None"));
            Assert.That(details.Severity, Is.EqualTo(InconsistencySeverity.Low));
        }

        [Test]
        public void GetInconsistencyDetails_DifferentStates_ReturnsInconsistencyDetails()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = 1000;

            // Act
            var details = _validator.GetInconsistencyDetails(state1, state2);

            // Assert
            Assert.That(details, Is.Not.Null);
            Assert.That(details.PropertyName, Is.Not.EqualTo("None"));
            Assert.That(details.ExpectedValue, Is.Not.Null);
            Assert.That(details.ActualValue, Is.Not.Null);
        }

        [Test]
        public void ValidateEngineStates_DifferentEngineTypes_ReturnsInconsistent()
        {
            // Arrange
            var guitarStats = CreateTestGuitarStats();
            var drumStats = CreateTestDrumStats();
            var states = new BaseStats[] { guitarStats, drumStats };

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.False);
            Assert.That(result.Inconsistencies.Length, Is.GreaterThan(0));
            
            // Should detect type mismatch
            var typeInconsistency = Array.Find(result.Inconsistencies, i => i.PropertyName == "Type");
            Assert.That(typeInconsistency, Is.Not.Null);
        }

        [Test]
        public void ValidateEngineStates_MultipleStates_DetectsAllInconsistencies()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            var state3 = CreateTestGuitarStats();
            
            state2.CommittedScore = 1000;
            state3.NotesHit = 50;
            
            var states = new BaseStats[] { state1, state2, state3 };

            // Act
            var result = _validator.ValidateEngineStates(states, DefaultTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.False);
            Assert.That(result.Inconsistencies.Length, Is.GreaterThan(0));
            
            // Should detect inconsistencies from both state2 and state3
            var scoreInconsistencies = Array.FindAll(result.Inconsistencies, i => i.PropertyName == "TotalScore");
            var notesInconsistencies = Array.FindAll(result.Inconsistencies, i => i.PropertyName == "NotesHit");
            
            Assert.That(scoreInconsistencies.Length, Is.GreaterThan(0));
            Assert.That(notesInconsistencies.Length, Is.GreaterThan(0));
        }

        [Test]
        public void ValidateEngineStates_LargeTolerance_AcceptsLargeDifferences()
        {
            // Arrange
            var state1 = CreateTestGuitarStats();
            var state2 = CreateTestGuitarStats();
            state2.CommittedScore = state1.CommittedScore + 100; // Large difference
            
            var states = new BaseStats[] { state1, state2 };
            var largeTolerance = 200.0;

            // Act
            var result = _validator.ValidateEngineStates(states, largeTolerance);

            // Assert
            Assert.That(result.IsConsistent, Is.True);
            Assert.That(result.Inconsistencies.Length, Is.EqualTo(0));
        }

        private GuitarStats CreateTestGuitarStats()
        {
            var stats = new GuitarStats();
            stats.CommittedScore = 400;
            stats.PendingScore = 100;
            stats.NotesHit = 25;
            stats.TotalNotes = 30;
            stats.Combo = 10;
            stats.MaxCombo = 15;
            stats.StarPowerTickAmount = 100;
            stats.StarPowerWhammyTicks = 50;
            stats.IsStarPowerActive = false;
            stats.Overstrums = 2;
            stats.HoposStrummed = 1;
            stats.GhostInputs = 0;
            return stats;
        }

        private DrumsStats CreateTestDrumStats()
        {
            var stats = new DrumsStats();
            stats.CommittedScore = 500;
            stats.PendingScore = 100;
            stats.NotesHit = 30;
            stats.TotalNotes = 33;
            stats.Combo = 12;
            stats.MaxCombo = 18;
            stats.StarPowerTickAmount = 120;
            stats.StarPowerWhammyTicks = 0;
            stats.IsStarPowerActive = false;
            stats.Overhits = 1;
            stats.GhostsHit = 2;
            stats.TotalGhosts = 5;
            stats.AccentsHit = 8;
            stats.TotalAccents = 10;
            stats.DynamicsBonus = 50;
            return stats;
        }
    }
}