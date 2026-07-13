using System.CommandLine;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using YARG.Core;
using YARG.Core.Chart.AutoGeneration;

namespace AutoDownchartCli;

internal static class Program
{
    private static readonly Dictionary<string, Instrument> Instruments = new(StringComparer.OrdinalIgnoreCase)
    {
        { "guitar", Instrument.FiveFretGuitar },
        { "coop", Instrument.FiveFretCoopGuitar },
        { "bass", Instrument.FiveFretBass },
        { "rhythm", Instrument.FiveFretRhythm },
        { "keys", Instrument.Keys },
    };

    public static int Main(string[] args)
    {
        var inputArgument = new Argument<string>("input")
        {
            Description = "Path to a .mid file or a directory containing notes.mid files",
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output file, or output root for directory mode",
        };

        var intensityOption = new Option<double>("--intensity")
        {
            Description = "Reduction intensity (0.0-1.0, default: 1.0)",
            DefaultValueFactory = _ => 1.0,
        };

        var instrumentOption = new Option<string?>("--instrument", "-i")
        {
            Description = "Comma-separated: guitar,coop,bass,rhythm,keys",
        };

        var replaceOption = new Option<bool>("--replace-existing")
        {
            Description = "Replace authored Hard, Medium, and Easy charts",
        };

        var inPlaceOption = new Option<bool>("--in-place")
        {
            Description = "Atomically replace each source MIDI",
        };

        var overwriteOption = new Option<bool>("--overwrite")
        {
            Description = "Replace existing output files",
        };

        var root = new RootCommand("Generate reduced MIDI difficulties from Expert five-fret charts")
        {
            inputArgument,
            outputOption,
            intensityOption,
            instrumentOption,
            replaceOption,
            inPlaceOption,
            overwriteOption,
        };

        root.Validators.Add(result =>
        {
            double intensity = result.GetValue(intensityOption);
            if (double.IsNaN(intensity) || double.IsInfinity(intensity) ||
                intensity < 0 || intensity > 1)
            {
                result.AddError("Intensity must be a number between 0 and 1.");
            }

            if (result.GetValue(inPlaceOption) && result.GetValue(outputOption) is not null)
            {
                result.AddError("--in-place cannot be combined with --output.");
            }

            string inputPath = result.GetValue(inputArgument)!;
            string? outputPath = result.GetValue(outputOption);
            if (outputPath is not null && File.Exists(inputPath))
            {
                var pathComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), pathComparison))
                {
                    result.AddError("--output cannot be the input file. Use --in-place instead.");
                }
            }

            string? instrumentValue = result.GetValue(instrumentOption);
            if (instrumentValue is not null)
            {
                string[] instrumentNames = instrumentValue.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (instrumentNames.Length == 0)
                {
                    result.AddError("--instrument must contain at least one instrument name.");
                }

                foreach (string name in instrumentNames)
                {
                    if (!Instruments.TryGetValue(name, out _))
                    {
                        result.AddError(
                            $"Unknown instrument '{name}'. Must be guitar, coop, bass, rhythm, or keys.");
                    }
                }
            }
        });

        root.SetAction(parseResult =>
        {
            var options = new CliOptions
            {
                InputPath = parseResult.GetValue(inputArgument)!,
                OutputPath = parseResult.GetValue(outputOption),
                Intensity = parseResult.GetValue(intensityOption),
                ReplaceExisting = parseResult.GetValue(replaceOption),
                InPlace = parseResult.GetValue(inPlaceOption),
                Overwrite = parseResult.GetValue(overwriteOption),
            };

            string? instrumentValue = parseResult.GetValue(instrumentOption);
            if (instrumentValue is not null)
            {
                foreach (string name in instrumentValue.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    options.Instruments.Add(Instruments[name]);
                }
            }

            return Run(options);
        });

        var parseResult = root.Parse(args);
        return parseResult.Invoke();
    }

    private static int Run(CliOptions options)
    {
        try
        {
            var inputs = GetInputs(options.InputPath);
            if (inputs.Count == 0)
            {
                Console.Error.WriteLine("No MIDI files were found.");
                return 1;
            }

            int failures = 0;
            int generated = 0;
            int skipped = 0;
            foreach (string input in inputs)
            {
                try
                {
                    var result = ProcessFile(input, options);
                    generated += result.GeneratedDifficultyCount;
                    skipped += result.SkippedDifficultyCount;
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"ERROR: {input}: {exception.Message}");
                }
            }

            Console.WriteLine($"Completed {inputs.Count - failures}/{inputs.Count} file(s): " +
                $"{generated} generated, {skipped} skipped, {failures} failed.");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static MidiDownchartExportResult ProcessFile(string inputPath, CliOptions options)
    {
        var source = MidFileLoader.LoadMidiFile(inputPath);
        var result = MidiDownchartExporter.Generate(source, new MidiDownchartExportOptions
        {
            Intensity = options.Intensity,
            ReplaceExisting = options.ReplaceExisting,
            Instruments = options.Instruments.Count == 0 ? null : options.Instruments,
        });

        if (result.GeneratedDifficultyCount == 0)
        {
            Console.WriteLine($"Skipped {inputPath}: no difficulties needed generation.");
            return result;
        }

        string outputPath = GetOutputPath(inputPath, options);
        if (!options.InPlace && File.Exists(outputPath) && !options.Overwrite)
        {
            throw new IOException($"Output already exists: {outputPath}. Use --overwrite to replace it.");
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (options.InPlace)
        {
            string temporaryPath = Path.Combine(
                directory ?? ".",
                $".{Path.GetFileName(inputPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                result.Midi.Write(temporaryPath, true);
                File.Move(temporaryPath, inputPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        else
        {
            result.Midi.Write(outputPath, options.Overwrite);
        }

        Console.WriteLine($"{inputPath} -> {outputPath}: " +
            $"{result.GeneratedDifficultyCount} generated, {result.SkippedDifficultyCount} skipped.");
        return result;
    }

    private static List<string> GetInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return [Path.GetFullPath(inputPath)];
        }

        if (Directory.Exists(inputPath))
        {
            return Directory.EnumerateFiles(inputPath, "notes.mid", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        throw new FileNotFoundException($"Input path does not exist: {inputPath}");
    }

    private static string GetOutputPath(string inputPath, CliOptions options)
    {
        if (options.InPlace)
        {
            return inputPath;
        }

        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            if (File.Exists(options.InputPath))
            {
                return Path.GetFullPath(options.OutputPath);
            }

            string relative = Path.GetRelativePath(Path.GetFullPath(options.InputPath), inputPath);
            string relativeDirectory = Path.GetDirectoryName(relative) ?? "";
            return Path.Combine(Path.GetFullPath(options.OutputPath), relativeDirectory, "notes.generated.mid");
        }

        string directory = Path.GetDirectoryName(inputPath) ?? ".";
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{fileName}.generated.mid");
    }

    private sealed class CliOptions
    {
        public string InputPath { get; init; } = "";
        public string? OutputPath { get; init; }
        public double Intensity { get; init; } = 1.0;
        public bool ReplaceExisting { get; init; }
        public bool InPlace { get; init; }
        public bool Overwrite { get; init; }
        public HashSet<Instrument> Instruments { get; } = [];
    }
}