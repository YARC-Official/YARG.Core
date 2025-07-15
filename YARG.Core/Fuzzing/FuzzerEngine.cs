using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Fuzzing.Models;
using YARG.Core.Fuzzing.InputGenerators;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Main orchestration engine for fuzzy testing of YARG.Core engines.
    /// </summary>
    public class FuzzerEngine
    {
        /// <summary>Configuration for the fuzzer</summary>
        public FuzzerConfiguration Configuration { get; }

        /// <summary>Frame timing generator component</summary>
        public IFrameTimingGenerator FrameTimingGenerator { get; }

        /// <summary>Input sequence generator component</summary>
        public IInputSequenceGenerator InputSequenceGenerator { get; }

        /// <summary>Consistency validator component</summary>
        public IConsistencyValidator ConsistencyValidator { get; }

        /// <summary>Test case minimizer component</summary>
        public ITestCaseMinimizer TestCaseMinimizer { get; private set; }

        /// <summary>
        /// Initializes a new instance of the FuzzerEngine.
        /// </summary>
        /// <param name="configuration">Fuzzer configuration</param>
        /// <param name="frameTimingGenerator">Frame timing generator</param>
        /// <param name="inputSequenceGenerator">Input sequence generator</param>
        /// <param name="consistencyValidator">Consistency validator</param>
        /// <param name="testCaseMinimizer">Test case minimizer (can be null initially)</param>
        public FuzzerEngine(
            FuzzerConfiguration configuration,
            IFrameTimingGenerator frameTimingGenerator,
            IInputSequenceGenerator inputSequenceGenerator,
            IConsistencyValidator consistencyValidator,
            ITestCaseMinimizer testCaseMinimizer)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FrameTimingGenerator = frameTimingGenerator ?? throw new ArgumentNullException(nameof(frameTimingGenerator));
            InputSequenceGenerator = inputSequenceGenerator ?? throw new ArgumentNullException(nameof(inputSequenceGenerator));
            ConsistencyValidator = consistencyValidator ?? throw new ArgumentNullException(nameof(consistencyValidator));
            TestCaseMinimizer = testCaseMinimizer; // Allow null for delayed initialization
        }

        /// <summary>
        /// Updates the test case minimizer component.
        /// </summary>
        /// <param name="testCaseMinimizer">New test case minimizer</param>
        public void UpdateTestCaseMinimizer(ITestCaseMinimizer testCaseMinimizer)
        {
            TestCaseMinimizer = testCaseMinimizer ?? throw new ArgumentNullException(nameof(testCaseMinimizer));
        }

        /// <summary>
        /// Runs fuzzing on a single test case.
        /// </summary>
        /// <param name="chart">Song chart to test</param>
        /// <param name="testCase">Test case to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Fuzzer result</returns>
        public Task<FuzzerResult> RunFuzzingAsync(SongChart chart, FuzzerTestCase testCase, CancellationToken cancellationToken = default)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            var stopwatch = Stopwatch.StartNew();
            var result = new FuzzerResult
            {
                TestCase = testCase,
                Statistics = new FuzzerStatistics()
            };

            try
            {
                YargLogger.LogInfo($"Starting fuzzer test case: {testCase.Name}");

                // Execute the fuzzing test with multiple frame timing patterns
                var inconsistencies = new List<InconsistencyDetails>();
                var executionCount = 0;
                var iterationTimes = new List<TimeSpan>();

                foreach (var frameTimingPattern in testCase.FrameTimingPatterns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (stopwatch.Elapsed > Configuration.MaxTestDuration)
                    {
                        result.TimedOut = true;
                        break;
                    }

                    // Execute multiple iterations with the same frame timing pattern
                    for (int iteration = 0; iteration < Configuration.TestIterationsPerScenario; iteration++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var iterationStopwatch = Stopwatch.StartNew();
                        
                        try
                        {
                            // Execute the test case with current frame timing pattern
                            var iterationInconsistencies = ExecuteTestIteration(chart, testCase, frameTimingPattern, cancellationToken);
                            inconsistencies.AddRange(iterationInconsistencies);
                            executionCount++;
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // Re-throw cancellation
                        }
                        catch (Exception ex)
                        {
                            YargLogger.LogWarning($"Iteration {iteration} failed: {ex.Message}");
                            // Continue with other iterations
                        }
                        finally
                        {
                            iterationStopwatch.Stop();
                            iterationTimes.Add(iterationStopwatch.Elapsed);
                        }

                        if (stopwatch.Elapsed > Configuration.MaxTestDuration)
                        {
                            result.TimedOut = true;
                            break;
                        }
                    }

                    if (result.TimedOut) break;
                }

                // Process results
                result.TotalExecutions = executionCount;
                result.Inconsistencies = inconsistencies.ToArray();
                result.Passed = inconsistencies.Count == 0;

                // Capture final engine stats from the last execution for display purposes
                if (executionCount > 0)
                {
                    try
                    {
                        // Execute one final time with regular timing to get clean final stats
                        var finalExecution = ExecuteEngineWithInputsAndStats(chart, testCase, testCase.InputSequence, FrameTimingPattern.Regular, cancellationToken);
                        if (finalExecution.HasValue)
                        {
                            result.FinalEngineStats = finalExecution.Value.Stats;
                        }
                    }
                    catch (Exception ex)
                    {
                        YargLogger.LogWarning($"Failed to capture final stats: {ex.Message}");
                    }
                }

                // Update statistics
                if (iterationTimes.Count > 0)
                {
                    result.Statistics.AverageIterationTime = TimeSpan.FromTicks((long)iterationTimes.Average(t => t.Ticks));
                    result.Statistics.MinExecutionTime = iterationTimes.Min();
                    result.Statistics.MaxExecutionTime = iterationTimes.Max();
                }
                result.Statistics.FrameTimingPatternsCount = testCase.FrameTimingPatterns.Length;
                result.Statistics.InputSequencesCount = 1; // One input sequence per test case
                result.Statistics.StateComparisonsCount = executionCount * testCase.FrameTimingPatterns.Length;

                // Generate detailed report
                result.DetailedReport = GenerateDetailedReport(testCase, result);
                
                // Generate reproduction commands if there are inconsistencies
                if (!result.Passed && inconsistencies.Count > 0)
                {
                    result.ReproductionCommand = GenerateReproductionCommand(testCase, result);
                }

                // Skip minimization for MVP - just log that we found inconsistencies
                if (inconsistencies.Count > 0)
                {
                    YargLogger.LogInfo($"Fuzzer: Found {inconsistencies.Count} inconsistencies - minimization disabled in MVP");
                }

                var status = result.Passed ? "PASSED" : "FAILED";
                YargLogger.LogInfo($"Completed fuzzer test case: {testCase.Name} - {status} ({inconsistencies.Count} inconsistencies)");
            }
            catch (OperationCanceledException)
            {
                result.TimedOut = true;
                YargLogger.LogWarning($"Fuzzer test case timed out: {testCase.Name}");
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                result.Passed = false;
                YargLogger.LogError($"Fuzzer test case failed: {testCase.Name} - {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Runs fuzzing on multiple test cases.
        /// </summary>
        /// <param name="chart">Song chart to test</param>
        /// <param name="testCases">Test cases to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of fuzzer results</returns>
        public async Task<FuzzerResult[]> RunBatchFuzzingAsync(SongChart chart, FuzzerTestCase[] testCases, CancellationToken cancellationToken = default)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (testCases == null) throw new ArgumentNullException(nameof(testCases));

            YargLogger.LogInfo($"Starting batch fuzzing with {testCases.Length} test cases");

            var results = new List<FuzzerResult>();

            if (Configuration.EnableParallelExecution)
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Configuration.MaxParallelThreads
                };

                var tasks = testCases.Select(testCase => RunFuzzingAsync(chart, testCase, cancellationToken));
                var completedResults = await Task.WhenAll(tasks);
                results.AddRange(completedResults);
            }
            else
            {
                foreach (var testCase in testCases)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await RunFuzzingAsync(chart, testCase, cancellationToken);
                    results.Add(result);
                }
            }

            YargLogger.LogInfo($"Completed batch fuzzing. {results.Count(r => r.Passed)} passed, {results.Count(r => !r.Passed)} failed");

            return results.ToArray();
        }

        /// <summary>
        /// Generates test cases for the specified chart and configuration.
        /// </summary>
        /// <param name="chart">Song chart to generate test cases for</param>
        /// <returns>Array of generated test cases</returns>
        public FuzzerTestCase[] GenerateTestCases(SongChart chart)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));

            var testCases = new List<FuzzerTestCase>();

            // Generate test cases for each target instrument and difficulty
            foreach (var instrumentName in Configuration.TargetInstruments)
            {
                if (!Enum.TryParse<Instrument>(instrumentName, out var instrument))
                    continue;

                foreach (var difficulty in Configuration.TargetDifficulties)
                {
                    // Generate focused test case
                    var (startTime, endTime) = GetChartTimeRange(chart, "StarPower");
                    var testCase = new FuzzerTestCase
                    {
                        Name = $"{instrument}_{difficulty}",
                        Chart = chart,
                        Instrument = instrument,
                        Difficulty = difficulty,
                        RandomSeed = Configuration.RandomSeed,
                        StartTime = startTime,
                        EndTime = endTime,
                        IsStarPowerFocused = true,
                        FrameTimingPatterns = FrameTimingGenerator.GetAvailablePatterns(),
                        InputSequence = GenerateInputSequenceForTimeRange(chart, instrument, difficulty, Configuration.RandomSeed, startTime, endTime, true)
                    };
                    testCases.Add(testCase);

                    // Generate general consistency test case
                    var (generalStartTime, generalEndTime) = GetChartTimeRange(chart, "General");
                    var generalTestCase = new FuzzerTestCase
                    {
                        Name = $"General_{instrument}_{difficulty}",
                        Chart = chart,
                        Instrument = instrument,
                        Difficulty = difficulty,
                        RandomSeed = Configuration.RandomSeed + 1000, // Different seed for variety
                        StartTime = generalStartTime,
                        EndTime = generalEndTime,
                        IsStarPowerFocused = false,
                        FrameTimingPatterns = FrameTimingGenerator.GetAvailablePatterns(),
                        InputSequence = GenerateInputSequenceForTimeRange(chart, instrument, difficulty, Configuration.RandomSeed + 1000, generalStartTime, generalEndTime, false)
                    };
                    testCases.Add(generalTestCase);
                }
            }

            YargLogger.LogInfo($"Generated {testCases.Count} test cases for chart");

            return testCases.ToArray();
        }

        /// <summary>
        /// Executes a single test iteration with the specified frame timing pattern.
        /// </summary>
        /// <param name="chart">Song chart</param>
        /// <param name="testCase">Test case</param>
        /// <param name="frameTimingPattern">Frame timing pattern to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of inconsistencies found</returns>
        private List<InconsistencyDetails> ExecuteTestIteration(SongChart chart, FuzzerTestCase testCase, 
            FrameTimingPattern frameTimingPattern, CancellationToken cancellationToken)
        {
            var inconsistencies = new List<InconsistencyDetails>();

            try
            {
                // Generate frame times for this iteration
                var frameTimes = FrameTimingGenerator.GenerateFrameTimes(testCase.StartTime, testCase.EndTime, frameTimingPattern);

                // Generate input sequence if not already provided
                var inputSequence = testCase.InputSequence?.Length > 0 
                    ? testCase.InputSequence 
                    : InputSequenceGenerator.GenerateRandomInputSequence(chart, testCase.Instrument, testCase.Difficulty, testCase.RandomSeed);
                
                // Filter inputs to ensure they're within the test case time bounds with a small buffer
                var originalInputCount = inputSequence.Length;
                var timeBuffer = 0.001; // 1ms buffer to handle floating-point precision
                inputSequence = inputSequence.Where(input => input.Time >= testCase.StartTime && input.Time <= (testCase.EndTime + timeBuffer))
                                           .OrderBy(input => input.Time)
                                           .ToArray();
                
                if (originalInputCount != inputSequence.Length)
                {
                    YargLogger.LogInfo($"Fuzzer: Filtered {originalInputCount - inputSequence.Length} inputs outside time bounds " +
                                     $"({testCase.StartTime:F3} - {testCase.EndTime:F3}) for test case {testCase.Name}");
                }
                
                // Log input time range for debugging
                if (inputSequence.Length > 0)
                {
                    var minInputTime = inputSequence.Min(i => i.Time);
                    var maxInputTime = inputSequence.Max(i => i.Time);
                    YargLogger.LogTrace($"Fuzzer: Input time range: {minInputTime:F6} - {maxInputTime:F6}, Test range: {testCase.StartTime:F6} - {testCase.EndTime:F6}");
                }

                // Run engine with regular timing (baseline)
                var baselineResult = ExecuteEngineWithInputsAndStats(chart, testCase, inputSequence, FrameTimingPattern.Regular, cancellationToken);
                
                // Run engine with test frame timing pattern
                var testResult = ExecuteEngineWithInputsAndStats(chart, testCase, inputSequence, frameTimingPattern, cancellationToken);

                // Compare engine states directly
                if (baselineResult.HasValue && testResult.HasValue)
                {
                    YargLogger.LogTrace($"Fuzzer: Comparing engine states directly");
                    var directComparison = ConsistencyValidator.ValidateEngineStates(
                        new[] { baselineResult.Value.Stats, testResult.Value.Stats }, 
                        Configuration.FloatingPointTolerance);
                    
                    YargLogger.LogTrace($"Fuzzer: Direct engine state comparison found {directComparison.Inconsistencies.Length} inconsistencies");
                    foreach (var inconsistency in directComparison.Inconsistencies)
                    {
                        inconsistency.ProblematicPattern = frameTimingPattern;
                        inconsistencies.Add(inconsistency);
                    }

                    // Also compare using ReplayAnalyzer if we have replay data
                    if (baselineResult.Value.Replay.HasValue && testResult.Value.Replay.HasValue)
                    {
                        var baselineAnalysis = AnalyzeReplay(chart, baselineResult.Value.Replay.Value.Info, baselineResult.Value.Replay.Value.Data);
                        var testAnalysis = AnalyzeReplay(chart, testResult.Value.Replay.Value.Info, testResult.Value.Replay.Value.Data);

                        // Compare analysis results
                        YargLogger.LogTrace($"Fuzzer: Comparing analysis results - baseline: {baselineAnalysis.Length} results, test: {testAnalysis.Length} results");
                        var comparisonInconsistencies = CompareAnalysisResults(baselineAnalysis, testAnalysis, frameTimingPattern);
                        YargLogger.LogTrace($"Fuzzer: Found {comparisonInconsistencies.Count} inconsistencies from analysis comparison");
                        inconsistencies.AddRange(comparisonInconsistencies);
                    }
                }
            }
            catch (Exception ex)
            {
                inconsistencies.Add(new InconsistencyDetails
                {
                    PropertyName = "ExecutionError",
                    Description = $"Error during test iteration: {ex.Message}",
                    Severity = InconsistencySeverity.High
                });
            }

            return inconsistencies;
        }

        /// <summary>
        /// Result of engine execution with inputs.
        /// </summary>
        private struct EngineExecutionResult
        {
            public BaseStats Stats;
            public (ReplayInfo Info, ReplayData Data)? Replay;
        }

        /// <summary>
        /// Executes the engine with the specified inputs and frame timing, returning both stats and replay.
        /// </summary>
        /// <param name="chart">Song chart</param>
        /// <param name="testCase">Test case</param>
        /// <param name="inputSequence">Input sequence to execute</param>
        /// <param name="frameTimingPattern">Frame timing pattern</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Engine execution result with stats and replay</returns>
        private EngineExecutionResult? ExecuteEngineWithInputsAndStats(SongChart chart, FuzzerTestCase testCase, 
            GameInput[] inputSequence, FrameTimingPattern frameTimingPattern, CancellationToken cancellationToken)
        {
            var replayData = ExecuteEngineWithInputs(chart, testCase, inputSequence, frameTimingPattern, cancellationToken);
            if (!replayData.HasValue) return null;

            // Extract the final stats from the replay data
            var replayFrame = replayData.Value.Data.Frames.LastOrDefault();
            if (replayFrame == null) return null;

            return new EngineExecutionResult
            {
                Stats = replayFrame.Stats,
                Replay = replayData
            };
        }

        /// <summary>
        /// Executes the engine with the specified inputs and frame timing, generating a replay.
        /// </summary>
        /// <param name="chart">Song chart</param>
        /// <param name="testCase">Test case</param>
        /// <param name="inputSequence">Input sequence to execute</param>
        /// <param name="frameTimingPattern">Frame timing pattern</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated replay data</returns>
        private (ReplayInfo Info, ReplayData Data)? ExecuteEngineWithInputs(SongChart chart, FuzzerTestCase testCase, 
            GameInput[] inputSequence, FrameTimingPattern frameTimingPattern, CancellationToken cancellationToken)
        {

            try
            {
                // Create a mock profile for the test
                var profile = CreateTestProfile(testCase.Instrument, testCase.Difficulty);
                
                // Create engine parameters
                var engineParameters = CreateEngineParameters(testCase.Instrument);
                
                // Create the engine
                var engine = CreateEngine(chart, profile, engineParameters);
                if (engine == null) return null;
                
                engine.SetSpeed(1.0f); // Standard speed for consistency testing
                engine.Reset();

                // Generate frame times based on the pattern
                var frameTimes = FrameTimingGenerator.GenerateFrameTimes(testCase.StartTime, testCase.EndTime, frameTimingPattern);
                
                // Ensure frame times don't exceed the test case end time
                frameTimes = frameTimes.Where(t => t <= testCase.EndTime).ToArray();
                
                YargLogger.LogTrace($"Fuzzer: Generated {frameTimes.Length} frame times for pattern {frameTimingPattern}, range: {testCase.StartTime:F6} - {testCase.EndTime:F6}");
                
                // Execute the engine with frame timing - using ReplayAnalyzer approach
                var replayInputs = new List<GameInput>();
                int currentInputIndex = 0;
                
                // Process frame by frame, similar to ReplayAnalyzer
                for (int frameIndex = 0; frameIndex < frameTimes.Length; frameIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    double frameTime = frameTimes[frameIndex];
                    
                    // Queue inputs that should happen before or at this frame time
                    while (currentInputIndex < inputSequence.Length && inputSequence[currentInputIndex].Time <= frameTime)
                    {
                        var input = inputSequence[currentInputIndex];
                        engine.QueueInput(ref input);
                        replayInputs.Add(input);
                        currentInputIndex++;
                    }
                    
                    // Update the engine to this frame time
                    engine.Update(frameTime);
                }
                
                // Queue any remaining inputs and do a final update to the end time
                var timeBuffer = 0.001; // 1ms buffer for floating-point precision
                while (currentInputIndex < inputSequence.Length)
                {
                    var input = inputSequence[currentInputIndex];
                    if (input.Time <= (testCase.EndTime + timeBuffer))
                    {
                        engine.QueueInput(ref input);
                        replayInputs.Add(input);
                    }
                    else
                    {
                        YargLogger.LogTrace($"Fuzzer: Skipping input at {input.Time:F6} (beyond end time {testCase.EndTime:F6})");
                    }
                    currentInputIndex++;
                }
                
                // Calculate the final update time to ensure all inputs are processed
                // Use the maximum of: test end time, last frame time, or last input time
                var maxFrameTime = frameTimes.Length > 0 ? frameTimes.Max() : testCase.StartTime;
                var maxInputTime = replayInputs.Count > 0 ? replayInputs.Max(i => i.Time) : testCase.StartTime;
                var finalUpdateTime = Math.Max(Math.Max(testCase.EndTime, maxFrameTime), maxInputTime) + timeBuffer;
                
                YargLogger.LogTrace($"Fuzzer: Final update time: {finalUpdateTime:F6} (test end: {testCase.EndTime:F6}, max frame: {maxFrameTime:F6}, max input: {maxInputTime:F6})");
                
                // Single final update - this should process all remaining inputs
                engine.Update(finalUpdateTime);
                
                // Capture stats before any potential cleanup
                var finalStats = engine.BaseStats;
                
                // Force clear the input queue to prevent warnings
                // This is a workaround for edge cases where inputs remain in the queue
                try
                {
                    var inputQueueField = engine.GetType().GetField("InputQueue", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (inputQueueField?.GetValue(engine) is Queue<GameInput> inputQueue && inputQueue.Count > 0)
                    {
                        YargLogger.LogTrace($"Fuzzer: Clearing {inputQueue.Count} remaining inputs from queue for test case {testCase.Name}");
                        inputQueue.Clear();
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogTrace($"Fuzzer: Could not clear input queue: {ex.Message}");
                }
                
                // Create replay data using the captured stats before reset
                var replayFrame = new ReplayFrame(profile, engineParameters, finalStats, replayInputs.ToArray());
                var replayData = new ReplayData(
                    new Dictionary<Guid, ColorProfile>(), 
                    new Dictionary<Guid, CameraPreset>(), 
                    new[] { replayFrame }, 
                    frameTimes);
                
                // Create replay info
                var replayInfo = new ReplayInfo(
                    path: $"fuzzer-test-{testCase.Name}-{frameTimingPattern}.replay",
                    replayName: $"Fuzzer Test {testCase.Name} {frameTimingPattern}",
                    replayVersion: 7,
                    engineVerion: 2, // Note: this is the actual parameter name (typo in original)
                    replayChecksum: HashWrapper.Hash(replayData.Serialize()),
                    song: "Fuzzer Test Song",
                    artist: "Fuzzer",
                    charter: "Fuzzer Engine",
                    songChecksum: HashWrapper.Hash(new byte[] { 1, 2, 3, 4 }), // Dummy checksum
                    date: DateTime.Now,
                    speed: 1.0f,
                    length: testCase.EndTime - testCase.StartTime,
                    score: finalStats.TotalScore,
                    stars: StarAmount.None, // We don't calculate stars in fuzzing
                    stats: new[] { CreateReplayStats(finalStats, testCase.Instrument) });
                
                return (replayInfo, replayData);
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to execute engine with inputs: {ex.Message}");
                return null;
            }
            finally
            {

            }
        }

        /// <summary>
        /// Creates a test profile for the specified instrument and difficulty.
        /// </summary>
        private YargProfile CreateTestProfile(Instrument instrument, Difficulty difficulty)
        {
            var profile = new YargProfile();
            profile.Name = "Fuzzer Test Profile";
            profile.CurrentInstrument = instrument;
            profile.CurrentDifficulty = difficulty;
            profile.IsBot = false; // We want human-like behavior for testing
            
            // Set appropriate game mode based on instrument
            profile.GameMode = instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => GameMode.FiveFretGuitar,
                Instrument.FourLaneDrums => GameMode.FourLaneDrums,
                Instrument.FiveLaneDrums => GameMode.FiveLaneDrums,
                Instrument.ProKeys => GameMode.ProKeys,
                Instrument.Vocals => GameMode.Vocals,
                _ => GameMode.FiveFretGuitar
            };
            
            return profile;
        }

        /// <summary>
        /// Creates engine parameters for the specified instrument.
        /// </summary>
        private BaseEngineParameters CreateEngineParameters(Instrument instrument)
        {
            // Create basic engine parameters using the same approach as EnginePreset
            var hitWindow = new HitWindowSettings(0.14, 0.14, 1.0, false, 0, 0, 0);
            var starMultiplierThresholds = new float[] { 10, 20, 30, 40 }; // Basic thresholds
            
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => new GuitarEngineParameters(
                    hitWindow,
                    4, // max multiplier
                    0.05, // whammy buffer
                    0.05, // sustain drop leniency
                    starMultiplierThresholds,
                    0.05, // hopo leniency
                    0.05, // strum leniency
                    0.05, // strum leniency small
                    false, // infinite front end
                    true, // anti ghosting
                    false // solo taps
                ),
                Instrument.FourLaneDrums or Instrument.FiveLaneDrums => new DrumsEngineParameters(
                    hitWindow,
                    4, // max multiplier
                    starMultiplierThresholds,
                    DrumsEngineParameters.DrumMode.NonProFourLane
                ),
                Instrument.ProKeys => new ProKeysEngineParameters(
                    hitWindow,
                    4, // max multiplier
                    0.05, // sustain drop leniency
                    0.05, // release leniency
                    starMultiplierThresholds,
                    0.05, // velocity threshold
                    0.05 // situational velocity window
                ),
                Instrument.Vocals => new VocalsEngineParameters(
                    hitWindow,
                    4, // max multiplier
                    starMultiplierThresholds,
                    1.7f, // pitch window E
                    1.7f, // pitch window M
                    0.05, // vocal hit window
                    0.05, // vocal sustain window
                    false, // percussion mode
                    60 // approximate vocal fps
                ),
                _ => new GuitarEngineParameters(
                    hitWindow,
                    4, // max multiplier
                    0.05, // whammy buffer
                    0.05, // sustain drop leniency
                    starMultiplierThresholds,
                    0.05, // hopo leniency
                    0.05, // strum leniency
                    0.05, // strum leniency small
                    false, // infinite front end
                    true, // anti ghosting
                    false // solo taps
                )
            };
        }

        /// <summary>
        /// Creates an engine instance for the specified instrument.
        /// </summary>
        private BaseEngine? CreateEngine(SongChart chart, YargProfile profile, BaseEngineParameters parameters)
        {
            try
            {
                switch (profile.GameMode)
                {
                    case GameMode.FiveFretGuitar:
                    {
                        var notes = chart.GetFiveFretTrack(profile.CurrentInstrument)
                            .GetDifficulty(profile.CurrentDifficulty).Clone();
                        profile.ApplyModifiers(notes);
                        
                        // Reset note states
                        foreach (var note in notes.Notes)
                        {
                            foreach (var subNote in note.AllNotes)
                            {
                                subNote.ResetNoteState();
                            }
                        }

                        return new YargFiveFretEngine(notes, chart.SyncTrack, (GuitarEngineParameters)parameters, profile.IsBot);
                    }
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                    {
                        var notes = chart.GetDrumsTrack(profile.CurrentInstrument)
                            .GetDifficulty(profile.CurrentDifficulty).Clone();
                        profile.ApplyModifiers(notes);
                        
                        // Reset note states
                        foreach (var note in notes.Notes)
                        {
                            foreach (var subNote in note.AllNotes)
                            {
                                subNote.ResetNoteState();
                            }
                        }

                        return new YargDrumsEngine(notes, chart.SyncTrack, (DrumsEngineParameters)parameters, profile.IsBot);
                    }
                    case GameMode.ProKeys:
                    {
                        var notes = chart.ProKeys.GetDifficulty(profile.CurrentDifficulty).Clone();
                        profile.ApplyModifiers(notes);
                        
                        // Reset note states
                        foreach (var note in notes.Notes)
                        {
                            foreach (var subNote in note.AllNotes)
                            {
                                subNote.ResetNoteState();
                            }
                        }

                        return new YargProKeysEngine(notes, chart.SyncTrack, (ProKeysEngineParameters)parameters, profile.IsBot);
                    }
                    case GameMode.Vocals:
                    {
                        var notes = chart.GetVocalsTrack(profile.CurrentInstrument)
                            .Parts[profile.HarmonyIndex].Clone();
                        profile.ApplyVocalModifiers(notes);

                        return new YargVocalsEngine(notes.CloneAsInstrumentDifficulty(), chart.SyncTrack, (VocalsEngineParameters)parameters, profile.IsBot);
                    }
                    default:
                        YargLogger.LogWarning($"Unsupported game mode for fuzzing: {profile.GameMode}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to create engine for {profile.GameMode}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates input sequence for the specified time range and focus.
        /// </summary>
        /// <param name="chart">Song chart</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <param name="seed">Random seed</param>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <param name="isStarPowerFocused">Whether to focus on star power</param>
        /// <returns>Generated input sequence</returns>
        private GameInput[] GenerateInputSequenceForTimeRange(SongChart chart, Instrument instrument, Difficulty difficulty, int seed, double startTime, double endTime, bool isStarPowerFocused)
        {
            if (isStarPowerFocused)
            {
                // Use the StarPowerInputGenerator directly with time bounds
                var starPowerGenerator = new StarPowerInputGenerator(seed);
                return starPowerGenerator.GenerateStarPowerFocusedInputs(chart, instrument, difficulty, startTime, endTime);
            }
            else
            {
                // Use the new overload with time bounds
                return InputSequenceGenerator.GenerateRandomInputSequence(chart, instrument, difficulty, seed, startTime, endTime);
            }
        }

        /// <summary>
        /// Gets the chart time range based on the configured coverage strategy.
        /// </summary>
        /// <param name="chart">Song chart</param>
        /// <param name="testType">Type of test (for logging purposes)</param>
        /// <returns>Start and end time for the test</returns>
        private (double startTime, double endTime) GetChartTimeRange(SongChart chart, string testType)
        {
            var chartStart = chart.GetStartTime();
            var chartEnd = chart.GetEndTime();
            var chartDuration = chartEnd - chartStart;

            switch (Configuration.CoverageStrategy)
            {
                case ChartCoverageStrategy.FullChart:
                    var maxDuration = Configuration.MaxChartDuration;
                    if (maxDuration > 0 && chartDuration > maxDuration)
                    {
                        YargLogger.LogInfo($"Chart duration ({chartDuration:F1}s) exceeds max duration ({maxDuration:F1}s), limiting to max duration for {testType} test");
                        return (chartStart, chartStart + maxDuration);
                    }
                    YargLogger.LogInfo($"Using full chart duration ({chartDuration:F1}s) for {testType} test");
                    return (chartStart, chartEnd);

                case ChartCoverageStrategy.TimeWindowed:
                    var windowSize = Configuration.TimeWindowSize;
                    var endTime = Math.Min(chartEnd, chartStart + windowSize);
                    YargLogger.LogInfo($"Using time window ({windowSize:F1}s) for {testType} test: {chartStart:F1}s - {endTime:F1}s");
                    return (chartStart, endTime);

                case ChartCoverageStrategy.Sampled:
                    // For sampled strategy, we'll return the full range but the actual sampling
                    // will be handled by the input generators
                    YargLogger.LogInfo($"Using sampled strategy for {testType} test across full chart duration ({chartDuration:F1}s)");
                    return (chartStart, chartEnd);

                default:
                    YargLogger.LogWarning($"Unknown coverage strategy {Configuration.CoverageStrategy}, defaulting to full chart");
                    return (chartStart, chartEnd);
            }
        }

        /// <summary>
        /// Creates replay stats from engine stats.
        /// </summary>
        private ReplayStats CreateReplayStats(BaseStats stats, Instrument instrument)
        {
            var playerName = "Fuzzer Test Profile";
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass => new GuitarReplayStats(playerName, (GuitarStats)stats),
                Instrument.FourLaneDrums or Instrument.FiveLaneDrums => new DrumsReplayStats(playerName, (DrumsStats)stats),
                Instrument.ProKeys => new ProKeysReplayStats(playerName, (ProKeysStats)stats),
                Instrument.Vocals => new VocalsReplayStats(playerName, (VocalsStats)stats),
                _ => new GuitarReplayStats(playerName, (GuitarStats)stats)
            };
        }

        /// <summary>
        /// Analyzes a replay using the ReplayAnalyzer.
        /// </summary>
        private AnalysisResult[] AnalyzeReplay(SongChart chart, ReplayInfo info, ReplayData data)
        {
            try
            {
                return ReplayAnalyzer.AnalyzeReplay(chart, info, data);
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to analyze replay: {ex.Message}");
                return Array.Empty<AnalysisResult>();
            }
        }

        /// <summary>
        /// Compares analysis results and returns inconsistencies.
        /// </summary>
        private List<InconsistencyDetails> CompareAnalysisResults(AnalysisResult[] baselineResults, 
            AnalysisResult[] testResults, FrameTimingPattern frameTimingPattern)
        {
            var inconsistencies = new List<InconsistencyDetails>();

            if (baselineResults.Length != testResults.Length)
            {
                inconsistencies.Add(new InconsistencyDetails
                {
                    PropertyName = "ResultCount",
                    Description = $"Different number of analysis results: baseline={baselineResults.Length}, test={testResults.Length}",
                    Severity = InconsistencySeverity.High,
                    ProblematicPattern = frameTimingPattern
                });
                return inconsistencies;
            }

            for (int i = 0; i < baselineResults.Length; i++)
            {
                var baseline = baselineResults[i];
                var test = testResults[i];

                // Compare if both passed/failed
                if (baseline.Passed != test.Passed)
                {
                    inconsistencies.Add(new InconsistencyDetails
                    {
                        PropertyName = $"Frame{i}.Passed",
                        Description = $"Analysis result mismatch: baseline={baseline.Passed}, test={test.Passed}",
                        ExpectedValue = baseline.Passed,
                        ActualValue = test.Passed,
                        Severity = InconsistencySeverity.High,
                        ProblematicPattern = frameTimingPattern
                    });
                }

                // Compare stats using the consistency validator
                if (baseline.ResultStats != null && test.ResultStats != null)
                {
                    var validationResult = ConsistencyValidator.ValidateEngineStates(
                        new[] { baseline.ResultStats, test.ResultStats }, 
                        Configuration.FloatingPointTolerance);
                    
                    foreach (var inconsistency in validationResult.Inconsistencies)
                    {
                        inconsistency.ProblematicPattern = frameTimingPattern;
                        inconsistencies.Add(inconsistency);
                    }
                }
            }

            return inconsistencies;
        }

        /// <summary>
        /// Generates a detailed report for the test results.
        /// </summary>
        /// <param name="testCase">Test case that was executed</param>
        /// <param name="result">Test result</param>
        /// <returns>Detailed report string</returns>
        private string GenerateDetailedReport(FuzzerTestCase testCase, FuzzerResult result)
        {
            var report = new List<string>
            {
                $"Fuzzer Test Report: {testCase.Name}",
                $"==========================================",
                $"Instrument: {testCase.Instrument}",
                $"Difficulty: {testCase.Difficulty}",
                $"Test Duration: {testCase.EndTime - testCase.StartTime:F2} seconds",
                $"Frame Timing Patterns: {string.Join(", ", testCase.FrameTimingPatterns)}",
                $"Input Sequence Length: {testCase.InputSequence.Length}",
                $"Random Seed: {testCase.RandomSeed}",
                $"",
                $"Execution Results:",
                $"- Total Executions: {result.TotalExecutions}",
                $"- Execution Time: {result.ExecutionTime.TotalSeconds:F2} seconds",
                $"- Status: {(result.Passed ? "PASSED" : "FAILED")}",
                $"- Inconsistencies Found: {result.Inconsistencies.Length}",
                $""
            };

            if (result.Statistics != null)
            {
                report.AddRange(new[]
                {
                    $"Performance Statistics:",
                    $"- Average Iteration Time: {result.Statistics.AverageIterationTime.TotalMilliseconds:F2} ms",
                    $"- Min Execution Time: {result.Statistics.MinExecutionTime.TotalMilliseconds:F2} ms",
                    $"- Max Execution Time: {result.Statistics.MaxExecutionTime.TotalMilliseconds:F2} ms",
                    $"- Frame Timing Patterns Tested: {result.Statistics.FrameTimingPatternsCount}",
                    $"- State Comparisons: {result.Statistics.StateComparisonsCount}",
                    $""
                });
            }

            if (result.Inconsistencies.Length > 0)
            {
                report.Add("Inconsistencies Found:");
                foreach (var inconsistency in result.Inconsistencies.Take(10)) // Show first 10
                {
                    report.Add($"- Property: {inconsistency.PropertyName}");
                    report.Add($"  Description: {inconsistency.Description}");
                    report.Add($"  Severity: {inconsistency.Severity}");
                    report.Add($"  Frame Pattern: {inconsistency.ProblematicPattern}");
                    
                    if (inconsistency.ExpectedValue != null && inconsistency.ActualValue != null)
                    {
                        report.Add($"  Expected: {inconsistency.ExpectedValue}");
                        report.Add($"  Actual: {inconsistency.ActualValue}");
                    }
                    
                    if (inconsistency.NumericalDifference != 0)
                    {
                        report.Add($"  Numerical Difference: {inconsistency.NumericalDifference:E6}");
                    }
                    
                    if (inconsistency.RelativeError != 0)
                    {
                        report.Add($"  Relative Error: {inconsistency.RelativeError:F4}%");
                    }
                    
                    report.Add(""); // Empty line between inconsistencies
                }
                
                if (result.Inconsistencies.Length > 10)
                {
                    report.Add($"... and {result.Inconsistencies.Length - 10} more inconsistencies");
                }
            }

            if (result.MinimalReproduction != null)
            {
                report.AddRange(new[]
                {
                    "",
                    "Minimal Reproduction Case:",
                    $"- Name: {result.MinimalReproduction.Name}",
                    $"- Minimal Inputs: {result.MinimalReproduction.MinimalInputs.Length}",
                    $"- Frame Timing: {result.MinimalReproduction.MinimalFrameTiming}",
                    "- Reproduction Steps:"
                });
                
                foreach (var step in result.MinimalReproduction.ReproductionSteps)
                {
                    report.Add($"  {step}");
                }
            }

            return string.Join(Environment.NewLine, report);
        }

        /// <summary>
        /// Generates a reproduction command for a failed test case.
        /// </summary>
        /// <param name="testCase">The test case that failed</param>
        /// <param name="result">The fuzzer result</param>
        /// <returns>CLI command string to reproduce the failure</returns>
        private string GenerateReproductionCommand(FuzzerTestCase testCase, FuzzerResult result)
        {
            var cmd = new System.Text.StringBuilder();
            cmd.Append("ReplayCli fuzz");
            cmd.Append($" --iterations 1");
            cmd.Append($" --seed {testCase.RandomSeed}");
            cmd.Append($" --instrument {testCase.Instrument}");
            cmd.Append($" --difficulty {testCase.Difficulty}");
            
            if (testCase.IsStarPowerFocused)
            {
                cmd.Append(" --focus-star-power");
            }
            
            cmd.Append(" --song \"<CHART_PATH>\"");
            cmd.Append($" # Reproduces {result.Inconsistencies.Length} inconsistencies");
            
            return cmd.ToString();
        }
    }
}