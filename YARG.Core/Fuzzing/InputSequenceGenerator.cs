using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Fuzzing.InputGenerators;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Base implementation of IInputSequenceGenerator for generating input sequences for fuzzy testing.
    /// </summary>
    public class InputSequenceGenerator : IInputSequenceGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;
        private readonly StarPowerInputGenerator _starPowerGenerator;
        private readonly WhammyInputGenerator _whammyGenerator;
        private readonly StarPowerActivationGenerator _activationGenerator;

        /// <summary>
        /// Initializes a new instance of InputSequenceGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public InputSequenceGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _starPowerGenerator = new StarPowerInputGenerator(seed);
            _whammyGenerator = new WhammyInputGenerator(seed);
            _activationGenerator = new StarPowerActivationGenerator(seed);
        }

        /// <summary>
        /// Generates an input sequence focused on star power mechanics.
        /// </summary>
        /// <param name="chart">Song chart to generate inputs for</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <returns>Array of game inputs</returns>
        public virtual GameInput[] GenerateStarPowerSequence(SongChart chart, Instrument instrument, Difficulty difficulty)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));

            return _starPowerGenerator.GenerateStarPowerFocusedInputs(chart, instrument, difficulty);
        }

        /// <summary>
        /// Generates a whammy input sequence with the specified pattern.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="pattern">Whammy pattern to use</param>
        /// <returns>Array of game inputs</returns>
        public virtual GameInput[] GenerateWhammySequence(double startTime, double endTime, WhammyPattern pattern)
        {
            return _whammyGenerator.GenerateWhammyInputs(startTime, endTime, pattern);
        }

        /// <summary>
        /// Generates a random input sequence for testing.
        /// </summary>
        /// <param name="chart">Song chart to generate inputs for</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <param name="seed">Random seed for reproducibility</param>
        /// <returns>Array of game inputs</returns>
        public virtual GameInput[] GenerateRandomInputSequence(SongChart chart, Instrument instrument, Difficulty difficulty, int seed)
        {
            return GenerateRandomInputSequence(chart, instrument, difficulty, seed, chart.GetStartTime(), chart.GetEndTime());
        }

        /// <summary>
        /// Generates a random input sequence for testing within specified time bounds.
        /// </summary>
        /// <param name="chart">Song chart to generate inputs for</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <param name="seed">Random seed for reproducibility</param>
        /// <param name="startTime">Start time for input generation</param>
        /// <param name="endTime">End time for input generation</param>
        /// <returns>Array of game inputs</returns>
        public virtual GameInput[] GenerateRandomInputSequence(SongChart chart, Instrument instrument, Difficulty difficulty, int seed, double startTime, double endTime)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));

            var random = new Random(seed);
            var inputs = new List<GameInput>();

            // TODO: Implement random input generation in subsequent tasks
            // This is a placeholder implementation for the core structure
            
            inputs.AddRange(GenerateRandomInputs(startTime, endTime, instrument, random));

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates basic input sequence for testing purposes.
        /// </summary>
        protected virtual GameInput[] GenerateBasicInputSequence(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();
            
            // Generate simple inputs every 0.5 seconds for basic testing
            for (double time = startTime; time < endTime; time += 0.5)
            {
                var input = CreateBasicInput(time, instrument);
                inputs.Add(input);
            }

            return inputs.ToArray();
        }



        /// <summary>
        /// Generates random inputs for the specified time range.
        /// </summary>
        protected virtual GameInput[] GenerateRandomInputs(double startTime, double endTime, Instrument instrument, Random random)
        {
            var inputs = new List<GameInput>();
            
            // Generate random inputs at random intervals
            for (double time = startTime; time < endTime; time += random.NextDouble() * 0.5 + 0.1)
            {
                var input = CreateRandomInput(time, instrument, random);
                inputs.Add(input);
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Creates a basic input for the specified instrument.
        /// </summary>
        protected virtual GameInput CreateBasicInput(double time, Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => 
                    GameInput.Create(time, GuitarAction.GreenFret, true),
                Instrument.FourLaneDrums => 
                    GameInput.Create(time, DrumsAction.RedDrum, 1.0f),
                Instrument.ProKeys => 
                    GameInput.Create(time, ProKeysAction.Key1, true),
                Instrument.Vocals => 
                    GameInput.Create(time, VocalsAction.Pitch, 440.0f), // A4 note
                _ => GameInput.Create(time, GuitarAction.GreenFret, true) // Default
            };
        }

        /// <summary>
        /// Gets a basic input value for the specified instrument.
        /// </summary>
        protected virtual int GetBasicInputValue(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => 1, // Green button
                Instrument.FourLaneDrums => 1, // Red pad
                Instrument.ProKeys => 1, // Key1
                Instrument.Vocals => 0,
                _ => 1 // Default
            };
        }

        /// <summary>
        /// Creates a random input for the specified instrument.
        /// </summary>
        protected virtual GameInput CreateRandomInput(double time, Instrument instrument, Random random)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => 
                    random.NextDouble() < 0.2 ? 
                        GameInput.Create(time, GuitarAction.Whammy, (float)random.NextDouble()) :
                        GameInput.Create(time, (GuitarAction)random.Next(0, 5), true), // Random fret
                Instrument.FourLaneDrums => 
                    GameInput.Create(time, (DrumsAction)random.Next(0, 8), (float)random.NextDouble()),
                Instrument.ProKeys => 
                    random.NextDouble() < 0.1 ? 
                        GameInput.Create(time, ProKeysAction.StarPower, true) :
                        GameInput.Create(time, (ProKeysAction)random.Next(0, 25), true), // Random key (Key1-Key25)
                Instrument.Vocals => 
                    GameInput.Create(time, VocalsAction.Pitch, 220.0f + (float)random.NextDouble() * 440.0f), // Random pitch
                _ => GameInput.Create(time, GuitarAction.GreenFret, true) // Default
            };
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}