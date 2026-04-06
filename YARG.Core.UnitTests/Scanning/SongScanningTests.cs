using NUnit.Framework;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    [Category("Integration")]
    public class SongScanningTests
    {
        private const string SONG_DIRECTORIES_ENV_VAR = "YARG_TEST_SONG_DIRS";

        private List<string> songDirectories;
        private readonly bool FULL_DIRECTORY_PATHS = false;
        private static readonly string SongCachePath = Path.Combine(Environment.CurrentDirectory, "songcache.bin");
        private static readonly string BadSongsPath = Path.Combine(Environment.CurrentDirectory, "badsongs.txt");

        [SetUp]
        public void Setup()
        {
            var configuredDirectories = Environment.GetEnvironmentVariable(SONG_DIRECTORIES_ENV_VAR);
            if (string.IsNullOrWhiteSpace(configuredDirectories))
            {
                Assert.Ignore($"Set {SONG_DIRECTORIES_ENV_VAR} to one or more song directories to run scanning tests.");
            }

            songDirectories = configuredDirectories
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Directory.Exists)
                .ToList();

            if (songDirectories.Count == 0)
            {
                Assert.Ignore($"No valid song directories were found in {SONG_DIRECTORIES_ENV_VAR}.");
            }
        }

        [TestCase]
        public void FullScan()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());
            var cache = CacheHandler.RunScan(false, SongCachePath, BadSongsPath, FULL_DIRECTORY_PATHS, songDirectories);
            // TODO: Any cache properties we want to check here?
            // Currently the only fail condition would be an unhandled exception
        }

        [TestCase]
        public void QuickScan()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());
            var cache = CacheHandler.RunScan(true, SongCachePath, BadSongsPath, FULL_DIRECTORY_PATHS, songDirectories);
            // TODO: see above
        }
    }
}
