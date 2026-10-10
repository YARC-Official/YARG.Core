using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using YARG.Core.Song.Cache;

namespace YARG.Core.Benchmarks
{
    /// <summary>
    /// The per-folder cost UltraStar discovery adds to every folder without a song.ini --
    /// point it at a non-UltraStar folder to check other formats aren't paying for it.
    /// </summary>
    [MemoryDiagnoser]
    public class FolderScanBenchmarks
    {
        public const string FOLDER_PATH_VAR = "TEST_FOLDER_PATH";

        private DirectoryInfo _folder;
        private FileCollection _collection;

        [GlobalSetup]
        public void Initialize()
        {
            string path = Environment.GetEnvironmentVariable(FOLDER_PATH_VAR)
                ?? throw new Exception("Could not find folder path environment variable!");
            _folder = new DirectoryInfo(path);
            _collection = new FileCollection(_folder);
        }

        [Benchmark(Description = "List folder")]
        public bool ListFolder() => new FileCollection(_folder).ContainsTextFiles;

        [Benchmark(Description = ".txt discovery, unconditional (previous)")]
        public int DiscoverUnconditionally() => _collection.FindAllFilesByExtension(".txt").Count;

        [Benchmark(Description = ".txt discovery, gated")]
        public int DiscoverGated() => _collection.ContainsTextFiles ? _collection.FindAllFilesByExtension(".txt").Count : 0;
    }
}
