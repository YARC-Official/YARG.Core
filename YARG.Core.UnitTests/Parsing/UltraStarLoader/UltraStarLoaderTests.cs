using System.Text;
using YARG.Core.Chart;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.Parsing
{
    internal class UltraStarLoaderTests
    {
        protected static readonly ParseSettings DefaultSettings = ParseSettings.Default;

        protected static FixedArray<byte> CreateUltraStarFile(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);

            using var ms = new MemoryStream(bytes);
            return FixedArray.Read(ms, bytes.Length);
        }

        protected static UltraStarLoader LoadUltraStar(string content)
        {
            using var file = CreateUltraStarFile(content);
            return new UltraStarLoader(file);
        }

        /// <summary>
        /// Helper to create UltraStar content in a cleaner way.
        /// Example: Us("BPM:120", ": 0 4 0 Ok")
        /// </summary>
        protected static string Us(params string[] lines)
        {
            return string.Join("\n", lines);
        }
    }
}
