using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.InputGenerators
{
    /// <summary>
    /// Generates guitar-specific input sequences for testing.
    /// </summary>
    public class GuitarInputGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;

        /// <summary>
        /// Initializes a new instance of GuitarInputGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public GuitarInputGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates guitar input patterns for the specified time range.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Guitar instrument type</param>
        /// <returns>Array of guitar inputs</returns>
        public GameInput[] GenerateGuitarInputs(double startTime, double endTime, Instrument instrument)
        {
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be less than end time");

            var inputs = new List<GameInput>();

            // Generate chord patterns
            inputs.AddRange(GenerateChordPatterns(startTime, endTime, instrument));
            
            // Generate single note patterns
            inputs.AddRange(GenerateSingleNotePatterns(startTime, endTime, instrument));
            
            // Generate strum patterns
            inputs.AddRange(GenerateStrumPatterns(startTime, endTime, instrument));
            
            // Generate whammy usage during sustained notes
            inputs.AddRange(GenerateWhammyDuringSustains(startTime, endTime, instrument));

            // Sort by time for proper ordering
            inputs.Sort((a, b) => a.Time.CompareTo(b.Time));

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates chord patterns for testing multi-fret combinations.
        /// </summary>
        private GameInput[] GenerateChordPatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double interval = 1.0; // 1 second between chords

            // Common chord combinations
            var chordCombinations = new[]
            {
                new[] { GuitarAction.GreenFret, GuitarAction.RedFret }, // Green + Red
                new[] { GuitarAction.RedFret, GuitarAction.YellowFret }, // Red + Yellow
                new[] { GuitarAction.YellowFret, GuitarAction.BlueFret }, // Yellow + Blue
                new[] { GuitarAction.BlueFret, GuitarAction.OrangeFret }, // Blue + Orange
                new[] { GuitarAction.GreenFret, GuitarAction.YellowFret, GuitarAction.OrangeFret }, // Green + Yellow + Orange
                new[] { GuitarAction.GreenFret, GuitarAction.RedFret, GuitarAction.YellowFret, GuitarAction.BlueFret, GuitarAction.OrangeFret } // All frets
            };

            for (double time = startTime; time < endTime; time += interval)
            {
                var chord = chordCombinations[_random.Next(chordCombinations.Length)];
                
                // Press all frets in the chord simultaneously
                foreach (var fret in chord)
                {
                    inputs.Add(GameInput.Create(time, fret, true));
                }
                
                // Add strum
                var strumAction = _random.NextDouble() < 0.5 ? GuitarAction.StrumDown : GuitarAction.StrumUp;
                inputs.Add(GameInput.Create(time + 0.01, strumAction, true)); // Slight delay for strum
                
                // Release frets after a short duration
                foreach (var fret in chord)
                {
                    inputs.Add(GameInput.Create(time + 0.5, fret, false));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates single note patterns for testing individual fret usage.
        /// </summary>
        private GameInput[] GenerateSingleNotePatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.25; // 4 notes per second

            var frets = new[] { GuitarAction.GreenFret, GuitarAction.RedFret, GuitarAction.YellowFret, GuitarAction.BlueFret, GuitarAction.OrangeFret };

            for (double time = startTime; time < endTime; time += interval)
            {
                var fret = frets[_random.Next(frets.Length)];
                
                // Press fret
                inputs.Add(GameInput.Create(time, fret, true));
                
                // Add strum
                var strumAction = _random.NextDouble() < 0.5 ? GuitarAction.StrumDown : GuitarAction.StrumUp;
                inputs.Add(GameInput.Create(time + 0.01, strumAction, true));
                
                // Release fret
                inputs.Add(GameInput.Create(time + 0.1, fret, false));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates strum patterns for testing strum timing precision.
        /// </summary>
        private GameInput[] GenerateStrumPatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double baseInterval = 0.5; // Base strum interval

            for (double time = startTime; time < endTime; time += baseInterval)
            {
                // Alternate between up and down strums
                var isUpStrum = ((int)((time - startTime) / baseInterval)) % 2 == 0;
                var strumAction = isUpStrum ? GuitarAction.StrumUp : GuitarAction.StrumDown;
                
                inputs.Add(GameInput.Create(time, strumAction, true));
                
                // Add slight timing variations for realism
                double variation = (_random.NextDouble() - 0.5) * 0.02; // ±10ms variation
                if (time + variation >= startTime && time + variation <= endTime)
                {
                    inputs.Add(GameInput.Create(time + variation, strumAction, false));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates whammy usage during sustained notes for star power testing.
        /// </summary>
        private GameInput[] GenerateWhammyDuringSustains(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double sustainDuration = 2.0; // 2-second sustains
            const double sustainInterval = 3.0; // 3 seconds between sustains

            for (double time = startTime; time < endTime; time += sustainInterval)
            {
                if (time + sustainDuration > endTime) break;

                // Start a sustained note
                var fret = GuitarAction.GreenFret; // Use green for simplicity
                inputs.Add(GameInput.Create(time, fret, true));
                inputs.Add(GameInput.Create(time + 0.01, GuitarAction.StrumDown, true));

                // Apply whammy during the sustain
                for (double whammyTime = time + 0.1; whammyTime < time + sustainDuration; whammyTime += 0.1)
                {
                    float whammyValue = (float)(0.5 + 0.5 * Math.Sin(2 * Math.PI * (whammyTime - time) / 0.5)); // Oscillating whammy
                    inputs.Add(GameInput.Create(whammyTime, GuitarAction.Whammy, whammyValue));
                }

                // End the sustained note
                inputs.Add(GameInput.Create(time + sustainDuration, fret, false));
                inputs.Add(GameInput.Create(time + sustainDuration, GuitarAction.Whammy, 0.0f)); // Stop whammy
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates rapid alternate picking patterns for stress testing.
        /// </summary>
        public GameInput[] GenerateAlternatePickingPattern(double startTime, double endTime, double bpm = 120.0)
        {
            var inputs = new List<GameInput>();
            double noteInterval = 60.0 / (bpm * 4); // 16th notes

            bool isUpStrum = false;
            var fret = GuitarAction.GreenFret;

            for (double time = startTime; time < endTime; time += noteInterval)
            {
                // Press fret
                inputs.Add(GameInput.Create(time, fret, true));
                
                // Alternate strum direction
                var strumAction = isUpStrum ? GuitarAction.StrumUp : GuitarAction.StrumDown;
                inputs.Add(GameInput.Create(time + 0.001, strumAction, true));
                
                // Release fret quickly
                inputs.Add(GameInput.Create(time + noteInterval * 0.8, fret, false));
                
                isUpStrum = !isUpStrum;
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates hammer-on/pull-off simulation patterns.
        /// </summary>
        public GameInput[] GenerateHammerOnPullOffPattern(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double interval = 1.0; // 1 second between patterns

            var fretPairs = new[]
            {
                (GuitarAction.GreenFret, GuitarAction.RedFret),
                (GuitarAction.RedFret, GuitarAction.YellowFret),
                (GuitarAction.YellowFret, GuitarAction.BlueFret),
                (GuitarAction.BlueFret, GuitarAction.OrangeFret)
            };

            for (double time = startTime; time < endTime; time += interval)
            {
                var (lowerFret, higherFret) = fretPairs[_random.Next(fretPairs.Length)];

                // Hammer-on: Start with lower fret, add higher fret without re-strumming
                inputs.Add(GameInput.Create(time, lowerFret, true));
                inputs.Add(GameInput.Create(time + 0.01, GuitarAction.StrumDown, true));
                inputs.Add(GameInput.Create(time + 0.2, higherFret, true)); // Hammer-on
                
                // Pull-off: Release higher fret, keep lower fret
                inputs.Add(GameInput.Create(time + 0.4, higherFret, false)); // Pull-off
                
                // Release all
                inputs.Add(GameInput.Create(time + 0.6, lowerFret, false));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}