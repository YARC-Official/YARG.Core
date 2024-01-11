using NUnit.Framework;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    public class SongScanningTests
    {
        private List<string> songDirectories;
        private readonly bool MULTITHREADING = true;
        private readonly bool ALLOW_DUPLICATES = true;
        private static readonly string SongCachePath = Path.Combine(Environment.CurrentDirectory, "songcache.bin");
        private static readonly string BadSongsPath = Path.Combine(Environment.CurrentDirectory, "badsongs.txt");

        [SetUp]
        public void Setup()
        {
            songDirectories = new()
            {
                
            };       
            Assert.That(songDirectories, Is.Not.Empty, "Add directories to scan for the test");
        }

        [TestCase]
        public void FullScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            var cache = CacheHandler.RunScan(false, SongCachePath, BadSongsPath, MULTITHREADING, ALLOW_DUPLICATES, songDirectories);
            // TODO: Any cache properties we want to check here?
            // Currently the only fail condition would be an unhandled exception
        }

        [TestCase]
        public void QuickScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            var cache = CacheHandler.RunScan(true, SongCachePath, BadSongsPath, MULTITHREADING, ALLOW_DUPLICATES, songDirectories);
            // TODO: see above
        }
    }
}
