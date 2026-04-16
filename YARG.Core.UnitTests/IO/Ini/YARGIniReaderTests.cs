using System.Text;
using NUnit.Framework;
using YARG.Core.IO.Ini;

namespace YARG.Core.UnitTests.IO.Ini;

public class YARGIniReaderTests
{
    private static readonly Dictionary<string, Dictionary<string, IniModifierOutline>> LOOKUPS = new()
    {
        ["[first]"] = new()
        {
            ["name"] = new("name", ModifierType.String),
            ["count"] = new("count", ModifierType.Int32),
        },
        ["[second]"] = new()
        {
            ["enabled"] = new("enabled", ModifierType.Bool),
        },
    };

    [Test]
    public void ReadIniFile_CollectsOnlyKnownSections()
    {
        var collections = ReadIni(
            """
            [first]
            name = Alpha

            [ignored]
            name = Ignored

            [second]
            enabled = 1
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collections.Keys, Is.EquivalentTo(["[first]", "[second]"]));
            Assert.That(collections["[first]"].Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Alpha"));
            Assert.That(collections["[second]"].Extract("enabled", out bool enabled), Is.True);
            Assert.That(enabled, Is.True);
        }
    }

    [Test]
    public void ReadIniFile_SkipsLeadingNonSectionLines()
    {
        var collections = ReadIni(
            """

            ; comment
            not a section

            [first]
            name = Alpha
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collections.ContainsKey("[first]"), Is.True);
            Assert.That(collections["[first]"].Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Alpha"));
        }
    }

    [Test]
    public void ReadIniFile_SkipsUnknownModifiersWithinKnownSection()
    {
        var collections = ReadIni(
            """
            [first]
            unknown = Ignored
            count = 12
            """
        );

        var modifiers = collections["[first]"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Contains("unknown"), Is.False);
            Assert.That(modifiers.Extract("count", out int count), Is.True);
            Assert.That(count, Is.EqualTo(12));
        }
    }

    [Test]
    public void ReadIniFile_RepeatedSectionUsesLatestOccurrence()
    {
        var collections = ReadIni(
            """
            [first]
            name = First

            [first]
            name = Second
            count = 5
            """
        );

        var modifiers = collections["[first]"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Second"));
            Assert.That(modifiers.Extract("count", out int count), Is.True);
            Assert.That(count, Is.EqualTo(5));
        }
    }

    [Test]
    public void ReadIniFile_ReturnsEmptyDictionaryWhenFileCannotBeRead()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        var collections = YARGIniReader.ReadIniFile(path, LOOKUPS);

        Assert.That(collections, Is.Empty);
    }

    private static Dictionary<string, IniModifierCollection> ReadIni(string iniContents, Encoding? encoding = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, iniContents, encoding ?? Encoding.UTF8);
            return YARGIniReader.ReadIniFile(path, LOOKUPS);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}