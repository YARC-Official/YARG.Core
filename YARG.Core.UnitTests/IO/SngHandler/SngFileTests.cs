using NUnit.Framework;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO.SngHandler;

public class SngFileTests
{
    [TestCase(1u, true)]
    [TestCase(0u, false)]
    [TestCase(2u, false)]
    public void TryLoadFromFile_LoadsOnlyVersionOne(uint version, bool loads)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sng");
        try
        {
            File.WriteAllBytes(path, CreateEmptySng(version));

            var sng = SngFile.TryLoadFromFile(path, true);
            bool loaded = sng.IsLoaded;
            if (loaded)
            {
                sng.Dispose();
            }

            Assert.That(loaded, Is.EqualTo(loads));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    // A .sng file with no metadata and no files
    private static byte[] CreateEmptySng(uint version)
    {
        using var stream = new MemoryStream();
        stream.Write("SNGPKG"u8);
        stream.Write(BitConverter.GetBytes(version));
        stream.Write(new byte[16]);                 // Mask
        stream.Write(BitConverter.GetBytes(8UL));   // Metadata length
        stream.Write(BitConverter.GetBytes(0UL));   // Metadata count
        stream.Write(BitConverter.GetBytes(8UL));   // File index length
        stream.Write(BitConverter.GetBytes(0UL));   // File count
        stream.Write(BitConverter.GetBytes(0UL));   // File data length
        return stream.ToArray();
    }
}
