using System;
using System.Collections.Generic;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.InputGenerators
{
    /// <summary>
    /// Generates drums-specific input sequences for testing.
    /// </summary>
    public class DrumsInputGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;

        /// <summary>
        /// Initializes a new instance of DrumsInputGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public DrumsInputGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates drum input patterns for the specified time range.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="instrument">Drum instrument type</param>
        /// <returns>Array of drum inputs</returns>
        public GameInput[] GenerateDrumInputs(double startTime, double endTime, Instrument instrument)
        {
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be less than end time");

            var inputs = new List<GameInput>();

            // Generate basic beat patterns
            inputs.AddRange(GenerateBasicBeatPattern(startTime, endTime, instrument));
            
            // Generate fill patterns
            inputs.AddRange(GenerateFillPatterns(startTime, endTime, instrument));
            
            // Generate kick pedal patterns
            inputs.AddRange(GenerateKickPatterns(startTime, endTime, instrument));
            
            // Generate cymbal patterns
            inputs.AddRange(GenerateCymbalPatterns(startTime, endTime, instrument));

            // Sort by time for proper ordering
            inputs.Sort((a, b) => a.Time.CompareTo(b.Time));

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates basic drum beat patterns.
        /// </summary>
        private GameInput[] GenerateBasicBeatPattern(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double beatInterval = 0.5; // 120 BPM (2 beats per second)

            for (double time = startTime; time < endTime; time += beatInterval)
            {
                // Basic rock beat: Kick on 1 and 3, snare on 2 and 4
                int beatNumber = (int)((time - startTime) / beatInterval) % 4;

                switch (beatNumber)
                {
                    case 0: // Beat 1 - Kick + Hi-hat
                    case 2: // Beat 3 - Kick + Hi-hat
                        inputs.Add(GameInput.Create(time, DrumsAction.Kick, GetRandomVelocity()));
                        inputs.Add(GameInput.Create(time, DrumsAction.YellowCymbal, GetRandomVelocity())); // Hi-hat
                        break;
                    case 1: // Beat 2 - Snare + Hi-hat
                    case 3: // Beat 4 - Snare + Hi-hat
                        inputs.Add(GameInput.Create(time, DrumsAction.RedDrum, GetRandomVelocity())); // Snare
                        inputs.Add(GameInput.Create(time, DrumsAction.YellowCymbal, GetRandomVelocity())); // Hi-hat
                        break;
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates drum fill patterns for testing complex combinations.
        /// </summary>
        private GameInput[] GenerateFillPatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double fillInterval = 8.0; // Fill every 8 seconds
            const double fillDuration = 1.0; // 1-second fills

            for (double time = startTime; time < endTime; time += fillInterval)
            {
                if (time + fillDuration > endTime) break;

                // Generate a tom fill pattern
                var toms = new[] { DrumsAction.RedDrum, DrumsAction.YellowDrum, DrumsAction.BlueDrum, DrumsAction.GreenDrum };
                const double noteInterval = 0.125; // 8th notes

                for (double fillTime = time; fillTime < time + fillDuration; fillTime += noteInterval)
                {
                    var tom = toms[_random.Next(toms.Length)];
                    inputs.Add(GameInput.Create(fillTime, tom, GetRandomVelocity()));
                }

                // End fill with crash cymbal
                inputs.Add(GameInput.Create(time + fillDuration, DrumsAction.YellowCymbal, 1.0f));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates kick pedal patterns for testing timing precision.
        /// </summary>
        private GameInput[] GenerateKickPatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();

            // Generate various kick patterns
            inputs.AddRange(GenerateDoubleKickPattern(startTime, endTime));
            inputs.AddRange(GenerateRapidKickPattern(startTime, endTime));
            inputs.AddRange(GenerateSubdivisionKickPattern(startTime, endTime));

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates double kick patterns.
        /// </summary>
        private GameInput[] GenerateDoubleKickPattern(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double patternInterval = 4.0; // Every 4 seconds
            const double doubleKickGap = 0.1; // 100ms between double kicks

            for (double time = startTime; time < endTime; time += patternInterval)
            {
                // Double kick
                inputs.Add(GameInput.Create(time, DrumsAction.Kick, GetRandomVelocity()));
                inputs.Add(GameInput.Create(time + doubleKickGap, DrumsAction.Kick, GetRandomVelocity()));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates rapid kick patterns for stress testing.
        /// </summary>
        private GameInput[] GenerateRapidKickPattern(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double patternInterval = 6.0; // Every 6 seconds
            const double rapidDuration = 1.0; // 1 second of rapid kicks
            const double rapidInterval = 0.05; // 20 kicks per second

            for (double time = startTime; time < endTime; time += patternInterval)
            {
                if (time + rapidDuration > endTime) break;

                for (double rapidTime = time; rapidTime < time + rapidDuration; rapidTime += rapidInterval)
                {
                    inputs.Add(GameInput.Create(rapidTime, DrumsAction.Kick, GetRandomVelocity()));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates subdivision kick patterns for timing precision testing.
        /// </summary>
        private GameInput[] GenerateSubdivisionKickPattern(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double beatInterval = 0.5; // 120 BPM

            // Generate kicks on various subdivisions
            var subdivisions = new[] { 1.0, 0.5, 0.25, 0.125 }; // Whole, half, quarter, eighth notes

            for (double time = startTime; time < endTime; time += beatInterval * 4) // Every measure
            {
                var subdivision = subdivisions[_random.Next(subdivisions.Length)];
                var interval = beatInterval * subdivision;

                for (double subTime = time; subTime < time + beatInterval * 4 && subTime < endTime; subTime += interval)
                {
                    if (_random.NextDouble() < 0.7) // 70% chance to hit
                    {
                        inputs.Add(GameInput.Create(subTime, DrumsAction.Kick, GetRandomVelocity()));
                    }
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates cymbal patterns for testing cymbal-specific mechanics.
        /// </summary>
        private GameInput[] GenerateCymbalPatterns(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            const double interval = 2.0; // Every 2 seconds

            var cymbals = instrument switch
            {
                Instrument.FourLaneDrums => new[] { DrumsAction.YellowCymbal, DrumsAction.BlueCymbal, DrumsAction.GreenCymbal },
                Instrument.FiveLaneDrums => new[] { DrumsAction.YellowCymbal, DrumsAction.OrangeCymbal },
                _ => new[] { DrumsAction.YellowCymbal }
            };

            for (double time = startTime; time < endTime; time += interval)
            {
                var cymbal = cymbals[_random.Next(cymbals.Length)];
                inputs.Add(GameInput.Create(time, cymbal, GetRandomVelocity()));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates ghost note patterns for velocity sensitivity testing.
        /// </summary>
        public GameInput[] GenerateGhostNotePattern(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.25; // Quarter note intervals

            for (double time = startTime; time < endTime; time += interval)
            {
                // Mix of normal hits and ghost notes (low velocity)
                bool isGhostNote = _random.NextDouble() < 0.3; // 30% chance
                float velocity = isGhostNote ? 0.1f + (float)_random.NextDouble() * 0.2f : // Ghost note: 0.1-0.3
                                              0.7f + (float)_random.NextDouble() * 0.3f;   // Normal hit: 0.7-1.0

                inputs.Add(GameInput.Create(time, DrumsAction.RedDrum, velocity)); // Snare
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates polyrhythm patterns for complex timing testing.
        /// </summary>
        public GameInput[] GeneratePolyrhythmPattern(double startTime, double endTime, int rhythm1 = 3, int rhythm2 = 4)
        {
            var inputs = new List<GameInput>();
            const double baseInterval = 0.5; // Base timing unit

            double pattern1Interval = baseInterval * rhythm2; // 3 against 4: every 2 beats
            double pattern2Interval = baseInterval * rhythm1; // 4 against 3: every 1.5 beats

            // Pattern 1 (e.g., 3 notes)
            for (double time = startTime; time < endTime; time += pattern1Interval)
            {
                for (int i = 0; i < rhythm1; i++)
                {
                    double noteTime = time + (i * pattern1Interval / rhythm1);
                    if (noteTime < endTime)
                    {
                        inputs.Add(GameInput.Create(noteTime, DrumsAction.RedDrum, GetRandomVelocity()));
                    }
                }
            }

            // Pattern 2 (e.g., 4 notes)
            for (double time = startTime; time < endTime; time += pattern2Interval)
            {
                for (int i = 0; i < rhythm2; i++)
                {
                    double noteTime = time + (i * pattern2Interval / rhythm2);
                    if (noteTime < endTime)
                    {
                        inputs.Add(GameInput.Create(noteTime, DrumsAction.Kick, GetRandomVelocity()));
                    }
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Gets a random velocity value for drum hits.
        /// </summary>
        private float GetRandomVelocity()
        {
            return 0.3f + (float)_random.NextDouble() * 0.7f; // Range: 0.3 to 1.0
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}