using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    public class SongScanningTests
    {
        private List<string> songDirectories = new();

        [SetUp]
        public void Setup()
        {
            Assert.That(songDirectories, Is.Not.Empty, "Add directories to scan for the test");
        }

        [TestCase]
        public void FullScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            CacheHandler handler = new(Environment.CurrentDirectory, Environment.CurrentDirectory, true, songDirectories.ToArray());
            SongCache cache;
            Assert.DoesNotThrow(() =>
            {
                cache = handler.RunScan(false);
            });
        }

        [TestCase]
        public void QuickScan()
        {
            YargTrace.AddListener(new YargDebugTraceListener());
            CacheHandler handler = new(Environment.CurrentDirectory, Environment.CurrentDirectory, false, songDirectories.ToArray());

            SongCache cache;
            Assert.DoesNotThrow(() =>
            {
                cache = handler.RunScan(true);
            });
        }
    }
}
