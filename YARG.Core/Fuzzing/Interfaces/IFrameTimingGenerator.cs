using System;

namespace YARG.Core.Fuzzing.Interfaces
{
    /// <summary>
    /// Interface for generating frame timing patterns for fuzzy testing.
    /// </summary>
    public interface IFrameTimingGenerator
    {
        /// <summary>
        /// Generates frame times for the specified time range using the given pattern.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="pattern">Frame timing pattern to use</param>
        /// <returns>Array of frame times in seconds</returns>
        double[] GenerateFrameTimes(double startTime, double endTime, FrameTimingPattern pattern);

        /// <summary>
        /// Gets all available frame timing patterns.
        /// </summary>
        /// <returns>Array of available patterns</returns>
        FrameTimingPattern[] GetAvailablePatterns();
    }

    /// <summary>
    /// Defines different frame timing patterns for testing.
    /// </summary>
    public enum FrameTimingPattern
    {
        /// <summary>Consistent frame times</summary>
        Regular,
        
        /// <summary>Random variations within bounds</summary>
        Irregular,
        
        /// <summary>Brief frame time spikes</summary>
        MicroStutters,
        
        /// <summary>Simulated FPS changes</summary>
        VariableFrameRate,
        
        /// <summary>High precision timing variations</summary>
        SubFramePrecision
    }
}