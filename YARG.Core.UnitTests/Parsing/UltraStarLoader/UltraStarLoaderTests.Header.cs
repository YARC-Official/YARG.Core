using NUnit.Framework;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.Parsing
{
    /// <summary>
    /// ScanHeader must report exactly what the full loader would -- every case checks both
    /// the expected literal and agreement with <c>new UltraStarLoader</c>.
    /// </summary>
    internal class UltraStarLoaderTests_Header : UltraStarLoaderTests
    {
        private static UltraStarHeader ScanBoth(string content, out UltraStarLoader loader)
        {
            using var file = CreateUltraStarFile(content);
            loader = new UltraStarLoader(file);
            return UltraStarLoader.ScanHeader(file);
        }

        [TestCase(new[] { ": 0 1 0 a" }, 1, TestName = "Notes without voice markers make one voice")]
        [TestCase(new[] { "P1", "P2" }, 0, TestName = "Voice markers alone create no voice")]
        [TestCase(new[] { "P 2", ": 0 1 0 a" }, 1, TestName = "A spaced voice marker is a marker")]
        [TestCase(new[] { "P1", ": 0 1 0 a", "P2", ": 0 1 0 b", "P3", ": 0 1 0 c" }, 3, TestName = "Three voices")]
        [TestCase(new[] { "P4", ": 0 1 0 a" }, 1, TestName = "P4 is ignored and notes stay in the current voice")]
        [TestCase(new[] { "P0", ": 0 1 0 a" }, 1, TestName = "P0 is ignored")]
        [TestCase(new[] { "P12", ": 0 1 0 a" }, 1, TestName = "P12 is not a voice marker")]
        [TestCase(new[] { "P３", ": 0 1 0 a" }, 1, TestName = "A fullwidth-digit marker is ignored")]
        [TestCase(new[] { "R 1 2", ": a b c" }, 0, TestName = "Malformed note lines create no voice")]
        [TestCase(new[] { ":\t0\t1\t0 x" }, 1, TestName = "A tab-separated note line parses")]
        [TestCase(new[] { "- 10" }, 0, TestName = "A rest alone creates no voice")]
        [TestCase(new[] { "-10" }, 0, TestName = "A rest written without a space creates no voice")]
        [TestCase(new[] { "-", "- x", "-x" }, 0, TestName = "A rest without a numeric beat creates no voice")]
        [TestCase(new[] { ": 0 1 0 a", "E", "P2", ": 0 1 0 b" }, 1, TestName = "Nothing after E counts")]
        [TestCase(new[] { ": 0 1 0 a", "E   ", "P2", ": 0 1 0 b" }, 1, TestName = "E with trailing spaces still ends the file")]
        [TestCase(new[] { "P2", "P1", ": 0 1 0 a" }, 1, TestName = "An empty voice before another doesn't count")]
        public void VoiceCountMatchesFullLoader(string[] body, int expected)
        {
            var header = ScanBoth(Us(new[] { "#TITLE:Song" }.Concat(body).ToArray()), out var loader);

            Assert.That(header.VoiceCount, Is.EqualTo(expected));
            Assert.That(header.VoiceCount, Is.EqualTo(loader.VoiceCount));
        }

        [TestCase("#TITLE:Song\n: 0 1 0 a", UltraStarFileKind.Chart, TestName = "A tag first line is a chart")]
        [TestCase("\n  \n\t\n#TITLE:Song\n: 0 1 0 a", UltraStarFileKind.Chart, TestName = "Blank leading lines are skipped")]
        [TestCase("#title:Song\n: 0 1 0 a", UltraStarFileKind.Chart, TestName = "A lowercase tag is a chart")]
        [TestCase("#TITLE :Song\n: 0 1 0 a", UltraStarFileKind.Chart, TestName = "A space before the colon is a chart")]
        [TestCase("#TITLE:\n: 0 1 0 a", UltraStarFileKind.Chart, TestName = "A tag with an empty value is a chart")]
        [TestCase("#TITLE:Song", UltraStarFileKind.NonChartText, TestName = "A tag line with no notes is not a chart")]
        [TestCase("# TITLE:Song\n: 0 1 0 a", UltraStarFileKind.NonChartText, TestName = "A non-tag '#' first line is not a chart even with notes")]
        [TestCase("# Pack notes\nThanks for downloading!", UltraStarFileKind.NonChartText, TestName = "A Markdown heading is not a chart")]
        [TestCase("# Notes: read me\n- item one", UltraStarFileKind.NonChartText, TestName = "A Markdown heading with a colon is not a chart")]
        [TestCase("Thanks for downloading!", UltraStarFileKind.NotAChart, TestName = "Plain text is not a chart")]
        [TestCase("", UltraStarFileKind.NotAChart, TestName = "An empty file is not a chart")]
        [TestCase("  \n\n", UltraStarFileKind.NotAChart, TestName = "A whitespace-only file is not a chart")]
        public void ClassifiesTextFiles(string content, UltraStarFileKind expected)
        {
            using var file = CreateUltraStarFile(content);
            Assert.That(UltraStarLoader.ClassifyTextFile(file.ToReferenceStream()), Is.EqualTo(expected));
        }

        [TestCase("utf-8")]
        [TestCase("utf-16")]
        [TestCase("utf-16BE")]
        [TestCase("utf-32")]
        public void ClassifiesChartsWithAnyByteOrderMark(string encodingName)
        {
            var encoding = System.Text.Encoding.GetEncoding(encodingName);
            byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes("#TITLE:Song\n: 0 1 0 a\n")).ToArray();
            using var ms = new MemoryStream(bytes);
            using var file = YARG.Core.IO.FixedArray.Read(ms, bytes.Length);

            Assert.That(UltraStarLoader.ClassifyTextFile(file.ToReferenceStream()), Is.EqualTo(UltraStarFileKind.Chart));
        }

        [Test]
        public void ClassifiesBomlessUtf16AsAChartSoItStillReportsAnError()
        {
            byte[] bytes = new System.Text.UnicodeEncoding(false, false).GetBytes("#TITLE:Song\n");
            using var ms = new MemoryStream(bytes);
            using var file = YARG.Core.IO.FixedArray.Read(ms, bytes.Length);

            Assert.That(UltraStarLoader.ClassifyTextFile(file.ToReferenceStream()), Is.EqualTo(UltraStarFileKind.Chart));
        }

        [TestCase("#TITLE:Song\n: 0 1 0 a", TestName = "A valid chart agrees")]
        [TestCase("#TITLE:Song", TestName = "A tags-only file agrees")]
        [TestCase("# Pack notes\nThanks for downloading!", TestName = "A Markdown file agrees")]
        [TestCase(": 0 1 0 a", TestName = "A notes-only file (no tag) agrees")]
        public void ClassificationAgreesWithHeaderVoiceCount(string content)
        {
            // Whenever the stream driver calls a file a Chart, the span-driver header scan
            // must also see at least one populated voice -- the two must never disagree
            // about which files get loaded.
            using var file = CreateUltraStarFile(content);
            var kind = UltraStarLoader.ClassifyTextFile(file.ToReferenceStream());
            var header = UltraStarLoader.ScanHeader(file);

            if (kind == UltraStarFileKind.Chart)
            {
                Assert.That(header.VoiceCount, Is.GreaterThan(0));
            }
        }

        [TestCase("TITLE", "Second", TestName = "A repeated tag keeps the last value")]
        [TestCase("ARTIST", "Someone", TestName = "A trailing comma is stripped")]
        [TestCase("GENRE", "Pop", TestName = "Tags after notes are still read")]
        [TestCase("YEAR", null, TestName = "Tags after E are not read")]
        [TestCase("K", null, TestName = "A tag with an empty value is rejected")]
        [TestCase("", null, TestName = "A tag with an empty key is rejected")]
        [TestCase("edition", "Lower", TestName = "Tag lookup ignores case")]
        public void MetadataMatchesFullLoader(string key, string? expected)
        {
            var header = ScanBoth(Us(
                "#TITLE:First",
                "#TITLE:Second",
                "#ARTIST:Someone,",
                "#EDITION:Lower",
                "#K:",
                "#:x",
                ": 0 1 0 a",
                "#GENRE:Pop",
                "E",
                "#YEAR:1999"
            ), out var loader);

            Assert.That(header.GetMetadata(key), Is.EqualTo(expected));
            Assert.That(header.GetMetadata(key), Is.EqualTo(loader.GetMetadata(key)));
        }
    }
}
