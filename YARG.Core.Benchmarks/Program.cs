using System;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace YARG.Core.Benchmarks
{
    public static class Program
    {
        public const string CHART_PATH_VAR = "TEST_CHART_PATH";

        public static void Main(string[] args)
        {
            // Non-interactive: "ultrastar <chart.txt>" or "folder <directory>".
            if (args.Length == 2)
            {
                RunFromArguments(args[0], args[1]);
                return;
            }

            ConsoleUtilities.WriteMenuHeader("YARG.Core Benchmarks", false);

            int choice = ConsoleUtilities.PromptChoice("Select a benchmark: ",
                "Chart Parsing",
                "Folder Scanning",
                "Playground",
                "Exit"
            );

            switch (choice)
            {
                case 0: ChartParsingBenchmark(); break;
                case 1: FolderScanBenchmark(); break;
                case 2: BenchmarkPlayground(); break;
                case 3: return;
            }
        }

        // In-process: the repo's .artifacts output layout puts the separate benchmark
        // project BenchmarkDotNet generates somewhere it doesn't look for it.
        private static readonly IConfig IN_PROCESS = DefaultConfig.Instance
            .AddJob(Job.Default.WithWarmupCount(3).WithIterationCount(10).WithToolchain(InProcessEmitToolchain.Instance));

        private static void RunFromArguments(string benchmark, string path)
        {
            switch (benchmark)
            {
                case "ultrastar":
                    Environment.SetEnvironmentVariable(CHART_PATH_VAR, path);
                    BenchmarkRunner.Run<UltraStarScanBenchmarks>(IN_PROCESS);
                    break;
                case "folder":
                    Environment.SetEnvironmentVariable(FolderScanBenchmarks.FOLDER_PATH_VAR, path);
                    BenchmarkRunner.Run<FolderScanBenchmarks>(IN_PROCESS);
                    break;
                default:
                    Console.WriteLine($"Unknown benchmark '{benchmark}'. Use 'ultrastar <chart.txt>' or 'folder <directory>'.");
                    break;
            }
        }

        private static void FolderScanBenchmark()
        {
            ConsoleUtilities.WriteMenuHeader("Folder Scanning Benchmark");

            string folderPath = ConsoleUtilities.PromptTextInput("Please enter a song folder path: ", (input) =>
                string.IsNullOrWhiteSpace(input) ? "Invalid input!" : !Directory.Exists(input) ? "Directory doesn't exist!" : null);

            RunFromArguments("folder", folderPath);
            ConsoleUtilities.WaitForKey("Press any key to exit...");
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
                if (Path.GetExtension(input) is not (".chart" or ".mid" or ".txt"))
                    return "Unsupported file type!";

                return null;
            });

            Environment.SetEnvironmentVariable(CHART_PATH_VAR, chartPath);
            Console.WriteLine();

            // A little unnecessary to split the file types into different tests, I suppose,
            // but why determine chart type repeatedly in the benchmark when you could do it once instead?
            string extension = Path.GetExtension(chartPath);
            switch (extension)
            {
                case ".chart":
                    BenchmarkRunner.Run<DotChartParsingBenchmarks>();
                    break;
                case ".mid":
                    BenchmarkRunner.Run<MidiParsingBenchmarks>();
                    break;
                case ".txt":
                    BenchmarkRunner.Run<UltraStarScanBenchmarks>(IN_PROCESS);
                    break;
            }

            ConsoleUtilities.WaitForKey("Press any key to exit...");
        }

        private static void BenchmarkPlayground()
        {
            BenchmarkRunner.Run<BenchmarkPlayground>();
            ConsoleUtilities.WaitForKey("Press any key to exit...");
        }
    }
}