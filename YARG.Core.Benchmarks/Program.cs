using System;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace YARG.Core.Benchmarks
{
    public static class Program
    {
        // Config to run benchmarks in-process, so that certain parameters can be set outside of benchmarking
        public static readonly IConfig BenchmarkConfig = DefaultConfig.Instance
            .AddJob(Job.Default
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .AsDefault());

        public static void Main()
        {
            ConsoleUtilities.WriteMenuHeader("YARG.Core Benchmarks", false);

            int choice = ConsoleUtilities.PromptChoice("Select a benchmark: ",
                "Chart Parsing",
                "Exit"
            );

            switch (choice)
            {
                case 0: ChartParsingBenchmark(); break;
                case 1: return;
            }
        }

        private static void ChartParsingBenchmark()
        {
            ConsoleUtilities.WriteMenuHeader("Chart Parsing Benchmark");

            string chartPath = ConsoleUtilities.PromptTextInput("Please enter a chart file path: ", (input) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return "Invalid input!";

                if (!File.Exists(input))
                    return "File doesn't exist!";

                // TODO: CON file detection, whenever that's supported by YARG.Core
                if (Path.GetExtension(input) is not (".chart" or ".mid"))
                    return "Unsupported file type!";

                return null;
            });

            Console.WriteLine();

            // A little unnecessary to split the file types into different tests, I suppose,
            // but why determine chart type repeatedly in the benchmark when you could do it once instead?
            string extension = Path.GetExtension(chartPath);
            switch (extension)
            {
                case ".chart":
                    DotChartParsingBenchmarks.ChartPath = chartPath;
                    BenchmarkRunner.Run<DotChartParsingBenchmarks>(BenchmarkConfig);
                    break;
                case ".mid":
                    MidiParsingBenchmarks.ChartPath = chartPath;
                    BenchmarkRunner.Run<MidiParsingBenchmarks>(BenchmarkConfig);
                    break;
            }

            ConsoleUtilities.WaitForKey("Press any key to exit...");
        }
    }
}