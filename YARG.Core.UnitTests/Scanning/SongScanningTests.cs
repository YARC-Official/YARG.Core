using NUnit.Framework;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    public class SongScanningTests
    {
        private List<string> songDirectories = new();
        private readonly bool MULTITHREADING = false;

        [SetUp]
        public void Setup()
        {
            Assert.That(songDirectories, Is.Not.Empty, "Add directories to scan for the test");
        }

        [TestCase]
        public void FullScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            CacheHandler handler = new(Environment.CurrentDirectory, Environment.CurrentDirectory, MULTITHREADING, songDirectories.ToArray());

            var cache = handler.RunScan(false);
            foreach (object err in handler.errorList)
                YargTrace.LogError(err.ToString());

            foreach (string log in handler.logs)
                YargTrace.LogInfo(log);
        }

        [TestCase]
        public void QuickScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            CacheHandler handler = new(Environment.CurrentDirectory, Environment.CurrentDirectory, MULTITHREADING, songDirectories.ToArray());

            var cache = handler.RunScan(true);
            foreach (object err in handler.errorList)
                YargTrace.LogError(err.ToString());

            foreach (string log in handler.logs)
                YargTrace.LogInfo(log);
        }
    }
}
