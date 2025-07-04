using System;
using System.Collections.Generic;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.InputGenerators
{
    /// <summary>
    /// Generates star power activation input sequences for testing activation timing.
    /// </summary>
    public class StarPowerActivationGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;

        /// <summary>
        /// Initializes a new instance of StarPowerActivationGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public StarPowerActivationGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates star power activation inputs at precise timing boundaries.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Target instrument</param>
        /// <returns>Array of star power activation inputs</returns>
        public GameInput[] GenerateActivationTimingTests(double startTime, double endTime, Instrument instrument)
        {
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be less than end time");

            var inputs = new List<GameInput>();

            // Test activation at frame boundaries (assuming 60 FPS)
            const double frameTime = 1.0 / 60.0;
            
            for (double time = startTime; time < endTime; time += frameTime * 5) // Every 5 frames
            {
                // Test activation exactly on frame boundary
                inputs.Add(CreateStarPowerActivation(time, instrument));
                
                // Test activation slightly before frame boundary
                if (time + frameTime * 0.1 < endTime)
                {
                    inputs.Add(CreateStarPowerActivation(time + frameTime * 0.1, instrument));
                }
                
                // Test activation slightly after frame boundary
                if (time + frameTime * 0.9 < endTime)
                {
                    inputs.Add(CreateStarPowerActivation(time + frameTime * 0.9, instrument));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates star power activation inputs with sub-frame precision timing.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Target instrument</param>
        /// <returns>Array of sub-frame precision activation inputs</returns>
        public GameInput[] GenerateSubFrameActivations(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double baseInterval = 1.0; // 1 second intervals
            const double maxOffset = 0.001; // ±1ms variation

            for (double time = startTime; time < endTime; time += baseInterval)
            {
                // Generate multiple activations with tiny timing differences
                for (int i = 0; i < 5; i++)
                {
                    double offset = (_random.NextDouble() - 0.5) * 2.0 * maxOffset;
                    double actualTime = time + offset;
                    
                    if (actualTime >= startTime && actualTime <= endTime)
                    {
                        inputs.Add(CreateStarPowerActivation(actualTime, instrument));
                    }
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates rapid star power activation/deactivation sequences for stress testing.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Target instrument</param>
        /// <returns>Array of rapid activation inputs</returns>
        public GameInput[] GenerateRapidActivations(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.1; // Very rapid - 10 times per second
            bool activating = true;

            for (double time = startTime; time < endTime; time += interval)
            {
                inputs.Add(CreateStarPowerActivation(time, instrument, activating));
                activating = !activating; // Toggle between activation and deactivation
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates star power activations at critical game state boundaries.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Target instrument</param>
        /// <returns>Array of boundary activation inputs</returns>
        public GameInput[] GenerateBoundaryActivations(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();

            // Critical timing boundaries
            var boundaries = new[]
            {
                startTime,
                startTime + 0.001, // Very early
                startTime + 0.1,   // Early
                (startTime + endTime) / 2, // Middle
                endTime - 0.1,     // Late
                endTime - 0.001,   // Very late
                endTime
            };

            foreach (var time in boundaries)
            {
                if (time >= startTime && time <= endTime)
                {
                    inputs.Add(CreateStarPowerActivation(time, instrument));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates star power activations with random timing for chaos testing.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="count">Number of random activations to generate</param>
        /// <returns>Array of random activation inputs</returns>
        public GameInput[] GenerateRandomActivations(double startTime, double endTime, Instrument instrument, int count = 10)
        {
            var inputs = new List<GameInput>();
            var duration = endTime - startTime;

            for (int i = 0; i < count; i++)
            {
                double randomTime = startTime + (_random.NextDouble() * duration);
                inputs.Add(CreateStarPowerActivation(randomTime, instrument));
            }

            // Sort by time for proper ordering
            inputs.Sort((a, b) => a.Time.CompareTo(b.Time));

            return inputs.ToArray();
        }

        /// <summary>
        /// Creates a star power activation input for the specified instrument.
        /// </summary>
        private GameInput CreateStarPowerActivation(double time, Instrument instrument, bool activate = true)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass =>
                    GameInput.Create(time, GuitarAction.StarPower, activate),
                Instrument.ProGuitar_17Fret or Instrument.ProGuitar_22Fret or 
                Instrument.ProBass_17Fret or Instrument.ProBass_22Fret =>
                    GameInput.Create(time, ProGuitarAction.StarPower, activate),
                Instrument.ProKeys =>
                    GameInput.Create(time, ProKeysAction.StarPower, activate),
                Instrument.Vocals =>
                    GameInput.Create(time, VocalsAction.StarPower, activate),
                _ => GameInput.Create(time, GuitarAction.StarPower, activate) // Default to guitar
            };
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}