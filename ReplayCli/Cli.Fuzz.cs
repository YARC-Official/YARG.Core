using System.Linq;
using YARG.Core;
using YARG.Core.Fuzzing;
using YARG.Core.Fuzzing.Models;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace ReplayCli;

public partial class Cli
{
    private bool RunFuzz()
    {
        var chart = ReadChart();
        if (chart is null)
        {
            return false;
        }

        Console.WriteLine("Starting fuzzy testing...");
        Console.WriteLine($"Chart: {_currentSongArtist} - {_currentSongName}");
        Console.WriteLine($"Iterations: {_fuzzIterations}");
        Console.WriteLine();

        StartLogging();

        try
        {
            // Create simple fuzzer configuration
            var config = CreateSimpleFuzzerConfiguration();
            
            // Create fuzzer engine components
            var frameTimingGenerator = new DefaultFrameTimingGenerator(config.MinFrameTime, config.MaxFrameTime, config.RandomSeed);
            var inputSequenceGenerator = new InputSequenceGenerator(config.RandomSeed);
            var consistencyValidator = new DefaultConsistencyValidator(config.FloatingPointTolerance);
            
            // Create fuzzer engine without minimizer
            var fuzzer = new FuzzerEngine(config, frameTimingGenerator, inputSequenceGenerator, consistencyValidator, null);
            
            // Log target instruments for verification
            Console.WriteLine($"Target instruments: {string.Join(", ", config.TargetInstruments)}");
            Console.WriteLine($"Target difficulties: {string.Join(", ", config.TargetDifficulties)}");
            Console.WriteLine($"Parallel execution: {(config.EnableParallelExecution ? $"Enabled ({config.MaxParallelThreads} threads)" : "Disabled")}");
            Console.WriteLine();
            
            // Generate test cases using the fuzzer engine
            var testCases = fuzzer.GenerateTestCases(chart);
            
            if (testCases.Length == 0)
            {
                Console.WriteLine("ERROR: No valid test cases could be generated for this chart!");
                Console.WriteLine("This may be because the chart doesn't contain the target instruments specified.");
                return false;
            }

            Console.WriteLine($"Generated {testCases.Length} test cases");
            Console.WriteLine();

            // Run fuzzing
            var results = RunBatchFuzzingSync(fuzzer, chart, testCases);
            
            // Process and display results
            return ProcessSimpleFuzzingResults(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Fuzzing failed with exception: {ex.Message}");
            YargLogger.LogException(ex, "Fuzzing failed");
            return false;
        }
        finally
        {
            StopLogging();
        }
    }

    private FuzzerConfiguration CreateSimpleFuzzerConfiguration()
    {
        var config = new FuzzerConfiguration();
        
        // Apply command line overrides
        config.TestIterationsPerScenario = _fuzzIterations;
        config.EnableTestCaseMinimization = false; // Disable minimization for MVP
        
        // Apply specific test parameters if provided
        if (_fuzzSeed >= 0)
        {
            config.RandomSeed = _fuzzSeed;
        }
        
        if (!string.IsNullOrEmpty(_fuzzInstrument))
        {
            config.TargetInstruments = new[] { _fuzzInstrument };
        }
        
        if (!string.IsNullOrEmpty(_fuzzDifficulty))
        {
            if (Enum.TryParse<Difficulty>(_fuzzDifficulty, out var difficulty))
            {
                config.TargetDifficulties = new[] { difficulty };
            }
        }
        
        // Apply parallel execution settings
        config.EnableParallelExecution = _fuzzParallel;
        config.MaxParallelThreads = _fuzzMaxThreads;
        
        return config;
    }

    private FuzzerResult[] RunBatchFuzzingSync(FuzzerEngine fuzzer, SongChart chart, FuzzerTestCase[] testCases)
    {
        // Run the async method synchronously
        var task = fuzzer.RunBatchFuzzingAsync(chart, testCases);
        task.Wait();
        return task.Result;
    }



    private bool ProcessSimpleFuzzingResults(FuzzerResult[] results)
    {
        int totalTests = results.Length;
        int passedTests = results.Count(r => r.Passed);
        int failedTests = totalTests - passedTests;
        
        Console.WriteLine("=== FUZZING RESULTS ===");
        Console.WriteLine($"Total test cases: {totalTests}");
        Console.WriteLine($"Passed: {passedTests}");
        Console.WriteLine($"Failed: {failedTests}");
        Console.WriteLine();
        
        if (failedTests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("INCONSISTENCIES DETECTED!");
            Console.ResetColor();
            Console.WriteLine();
            
            foreach (var result in results.Where(r => !r.Passed))
            {
                PrintSimpleFailedTestResult(result);
            }
            
            return false;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ALL TESTS PASSED!");
            Console.WriteLine("No engine inconsistencies detected.");
            Console.ResetColor();
            
            return true;
        }
    }

    private void PrintSimpleFailedTestResult(FuzzerResult result)
    {
        Console.WriteLine($"FAILED: {result.TestCase.Name}");
        Console.WriteLine($"Execution time: {result.ExecutionTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Total executions: {result.TotalExecutions}");
        Console.WriteLine($"Inconsistencies found: {result.Inconsistencies.Length}");
        
        foreach (var inconsistency in result.Inconsistencies.Take(5)) // Show first 5
        {
            Console.WriteLine($"  - {inconsistency.PropertyName}: Expected {inconsistency.ExpectedValue}, Got {inconsistency.ActualValue}");
            if (inconsistency.NumericalDifference != 0)
            {
                Console.WriteLine($"    Difference: {inconsistency.NumericalDifference:F6}");
            }
        }
        
        if (result.Inconsistencies.Length > 5)
        {
            Console.WriteLine($"  ... and {result.Inconsistencies.Length - 5} more");
        }
        
        // Display simple reproduction command
        if (!string.IsNullOrEmpty(result.ReproductionCommand))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("REPRODUCTION COMMAND:");
            Console.ResetColor();
            var command = result.ReproductionCommand.Replace("\"<CHART_PATH>\"", $"\"{_songPath}\"");
            Console.WriteLine(command);
        }
        
        Console.WriteLine();
    }


}