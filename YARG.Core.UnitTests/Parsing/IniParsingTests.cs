using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.IO.Ini;

namespace YARG.Core.UnitTests.Parsing;

public class IniParsingTests
{
    private const string MULTIPLE_EQUALS_TEXT =
        """
        [song]
        key = value=with = equals
        link_youtube = https://youtube.com/watch?v=dQw4w9WgXcQ
        """;

    private FixedArray<byte>? _text;

    [Test]
    public void LineWithMultipleEquals()
    {
        CreateTextContainer();
        var textContainer = new YARGTextContainer<byte>(_text!, Encoding.UTF8);
        var keyOutline = new IniModifierOutline("key", ModifierType.String);
        var youtubeOutline = new IniModifierOutline("link_youtube", ModifierType.String);

        var collection = new IniModifierCollection();
        collection.Add(ref textContainer, keyOutline, false);
        collection.Add(ref textContainer, youtubeOutline, false);

        var result = YARGIniReader.ReadIniFile(_text, GetIniLookupMap());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TryGetValue("[song]", out var modifiers), Is.True);
            Assert.That(modifiers!.Extract("key", out string key), Is.True);
            Assert.That(key, Is.EqualTo("value=with = equals"));

            Assert.That(modifiers.Extract("link_youtube", out string youtubeLink), Is.True);
            Assert.That(youtubeLink, Is.EqualTo("https://youtube.com/watch?v=dQw4w9WgXcQ"));
        }
    }

    private static Dictionary<string, Dictionary<string, IniModifierOutline>> GetIniLookupMap()
    {
        return new()
        {
            { "[song]", new()
                {
                    { "key", new("key", ModifierType.String) },
                    { "link_youtube", new("link_youtube", ModifierType.String) }
                }
            }
        };
    }

    private void CreateTextContainer()
    {
        _text = FixedArray<byte>.Alloc(Encoding.UTF8.GetByteCount(MULTIPLE_EQUALS_TEXT));
        Encoding.UTF8.GetBytes(MULTIPLE_EQUALS_TEXT, _text.Span);
    }

    [TearDown]
    public void DisposeTextContainer()
    {
        if (_text is null) return;
        _text.Dispose();
    }
}