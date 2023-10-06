using NUnit.Framework;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    public class SongScanningTests
    {
        private List<string> songDirectories;
        private readonly bool MULTITHREADING = true;
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
            CacheHandler handler = new(songDirectories);
            var cache = handler.RunScan(false, SongCachePath, BadSongsPath, MULTITHREADING);
            // TODO: Any cache properties we want to check here?
            // Currently the only fail condition would be an unhandled exception
        }

        [TestCase]
        public void QuickScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            CacheHandler handler = new(songDirectories);
            var cache = handler.RunScan(true, SongCachePath, BadSongsPath, MULTITHREADING);
            // TODO: see above
        }
    }
}
