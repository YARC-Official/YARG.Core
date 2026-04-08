using NUnit.Framework;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.UnitTests.Song;

public class SortStringTests
{
    [Test]
    public void Constructor_NormalizesMetadataForSearchAndSort()
    {
        var sortString = new SortString("  <b>The   Ænema\t</b>  ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sortString.Original, Is.EqualTo("  <b>The   Ænema\t</b>  "));
            Assert.That(sortString.SearchStr, Is.EqualTo("the aenema"));
            Assert.That(sortString.SortStr, Is.EqualTo("aenema"));
            Assert.That(sortString.Group, Is.EqualTo(CharacterGroup.AsciiLetter));
            Assert.That(sortString.Length, Is.EqualTo(sortString.Original.Length));
            Assert.That(sortString.ToString(), Is.EqualTo(sortString.Original));
        }
    }

    [TestCase("The Beatles", "beatles")]
    [TestCase("El Final", "final")]
    [TestCase("La Bamba", "bamba")]
    [TestCase("Le Temps", "temps")]
    [TestCase("Les Wampas", "wampas")]
    [TestCase("Los Fabulosos Cadillacs", "fabulosos cadillacs")]
    public void Constructor_RemovesSupportedLeadingArticles(string input, string expectedSortStr)
    {
        var sortString = new SortString(input);

        Assert.That(sortString.SortStr, Is.EqualTo(expectedSortStr));
    }

    [Test]
    public void Constructor_DoesNotRemoveArticleFromMiddleOfString()
    {
        var sortString = new SortString("Meet the Beatles");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sortString.SearchStr, Is.EqualTo("meet the beatles"));
            Assert.That(sortString.SortStr, Is.EqualTo("meet the beatles"));
        }
    }

    [TestCase("", CharacterGroup.Empty)]
    [TestCase("!intro", CharacterGroup.AsciiSymbol)]
    [TestCase("2fast", CharacterGroup.AsciiNumber)]
    [TestCase("Alpha", CharacterGroup.AsciiLetter)]
    [TestCase("中華", CharacterGroup.NonAscii)]
    public void Constructor_AssignsCharacterGroupFromNormalizedSortString(string input, CharacterGroup expectedGroup)
    {
        var sortString = new SortString(input);

        Assert.That(sortString.Group, Is.EqualTo(expectedGroup));
    }

    [Test]
    public void CompareTo_TreatsEquivalentNormalizedValuesAsEqual()
    {
        var left = new SortString("  <b>The   Béatles</b>  ");
        var right = new SortString("beatles");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(left.CompareTo(right), Is.Zero);
            Assert.That(right.CompareTo(left), Is.Zero);
        }
    }

    [Test]
    public void CompareTo_UsesCharacterGroupBeforeLexicalOrder()
    {
        var values = new[]
        {
            new SortString(""),
            new SortString("#hash"),
            new SortString("12 bars"),
            new SortString("alpha"),
            new SortString("中華"),
        };

        for (int i = 0; i < values.Length - 1; i++)
        {
            Assert.That(values[i].CompareTo(values[i + 1]), Is.LessThan(0));
        }
    }

    [Test]
    public void CompareTo_UsesOrdinalComparisonWithinSameCharacterGroup()
    {
        var alpha = new SortString("Alpha");
        var beta = new SortString("Beta");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(alpha.CompareTo(beta), Is.LessThan(0));
            Assert.That(beta.CompareTo(alpha), Is.GreaterThan(0));
        }
    }
}
