using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    public class LyricTests
    {
        private const string STRIP_TEST_STRING = "a-b=c+d#e^f*g%h/i$j§k_l";

        [TestCase]
        public void StripForLyrics()
        {
            const string STRIPPED_LYRICS = "ab-cdefghij k l";
            Assert.That(LyricSymbols.StripForLyrics(STRIP_TEST_STRING), Is.EqualTo(STRIPPED_LYRICS));
        }

        [TestCase]
        public void StripForVocals()
        {
            const string STRIPPED_VOCALS = "a-b-cdefghij‿k l";
            Assert.That(LyricSymbols.StripForVocals(STRIP_TEST_STRING), Is.EqualTo(STRIPPED_VOCALS));
        }
    }
}