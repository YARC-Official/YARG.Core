using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.Interfaces
{
    /// <summary>
    /// Interface for generating input sequences for fuzzy testing.
    /// </summary>
    public interface IInputSequenceGenerator
    {
        /// <summary>
        /// Generates an input sequence focused on star power mechanics.
        /// </summary>
        /// <param name="chart">Song chart to generate inputs for</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <returns>Array of game inputs</returns>
        GameInput[] GenerateStarPowerSequence(SongChart chart, Instrument instrument, Difficulty difficulty);

        /// <summary>
        /// Generates a whammy input sequence with the specified pattern.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="pattern">Whammy pattern to use</param>
        /// <returns>Array of game inputs</returns>
        GameInput[] GenerateWhammySequence(double startTime, double endTime, WhammyPattern pattern);

        /// <summary>
        /// Generates a random input sequence for testing.
        /// </summary>
        /// <param name="chart">Song chart to generate inputs for</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <param name="seed">Random seed for reproducibility</param>
        /// <returns>Array of game inputs</returns>
        GameInput[] GenerateRandomInputSequence(SongChart chart, Instrument instrument, Difficulty difficulty, int seed);

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
        GameInput[] GenerateRandomInputSequence(SongChart chart, Instrument instrument, Difficulty difficulty, int seed, double startTime, double endTime);
    }

    /// <summary>
    /// Defines different whammy input patterns for testing.
    /// </summary>
    public enum WhammyPattern
    {
        /// <summary>Steady whammy input</summary>
        Continuous,
        
        /// <summary>On/off whammy patterns</summary>
        Intermittent,
        
        /// <summary>Fast whammy changes</summary>
        RapidToggle,
        
        /// <summary>Whammy changes between frames</summary>
        SubFrameTiming,
        
        /// <summary>Whammy at critical timing boundaries</summary>
        EdgeCaseTiming
    }
}