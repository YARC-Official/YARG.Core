using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.IO.Ini;

namespace YARG.Core.UnitTests.IO.Ini;

public class IniModifierCollectionTests
{
    [Test]
    public void Add_StoresRepresentativeValuesAndExtractRemovesThem()
    {
        var collection = new IniModifierCollection();

        AddModifier(collection, "title_text", "Alpha", ModifierType.String);
        AddModifier(collection, "count_number", "12", ModifierType.Int32);
        AddModifier(collection, "enabled_flag", "1", ModifierType.Bool);
        AddModifier(collection, "speed_value", "2.5", ModifierType.Double);
        AddModifier(collection, "preview_range", "1000 2500", ModifierType.Int64Array);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection.Contains("title_text"), Is.True);
            Assert.That(collection.Contains("count_number"), Is.True);
            Assert.That(collection.Contains("enabled_flag"), Is.True);
            Assert.That(collection.Contains("speed_value"), Is.True);
            Assert.That(collection.Contains("preview_range"), Is.True);

            Assert.That(collection.Extract("title_text", out string title), Is.True);
            Assert.That(title, Is.EqualTo("Alpha"));
            Assert.That(collection.Extract("count_number", out int count), Is.True);
            Assert.That(count, Is.EqualTo(12));
            Assert.That(collection.Extract("enabled_flag", out bool enabled), Is.True);
            Assert.That(enabled, Is.True);
            Assert.That(collection.Extract("speed_value", out double speed), Is.True);
            Assert.That(speed, Is.EqualTo(2.5).Within(0.0000001));
            Assert.That(collection.Extract("preview_range", out (long, long) preview), Is.True);
            Assert.That(preview, Is.EqualTo((1000L, 2500L)));

            Assert.That(collection.Contains("title_text"), Is.False);
            Assert.That(collection.IsEmpty(), Is.True);
        }
    }

    [Test]
    public void Add_UsesCurrentFallbackValuesWhenParsingFails()
    {
        var collection = new IniModifierCollection();

        AddModifier(collection, "bad_int", "not a number", ModifierType.Int32);
        AddModifier(collection, "bad_double", "not a decimal", ModifierType.Double);
        AddModifier(collection, "bad_range", "invalid", ModifierType.Int64Array);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection.Extract("bad_int", out int integer), Is.True);
            Assert.That(integer, Is.Zero);
            Assert.That(collection.Extract("bad_double", out double floatingPoint), Is.True);
            Assert.That(floatingPoint, Is.Zero);
            Assert.That(collection.Extract("bad_range", out (long, long) range), Is.True);
            Assert.That(range, Is.EqualTo((-1L, -1L)));
        }
    }

    [Test]
    public void AddSng_UsesSpecifiedStringLengthAndParsesInt64ArraysFromCurrentPosition()
    {
        var collection = new IniModifierCollection();

        AddSngModifier(collection, "short_name", "Alphabet", ModifierType.String, 5);
        AddSngModifier(collection, "sng_range", "3000 4500", ModifierType.Int64Array);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection.Extract("short_name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Alpha"));
            Assert.That(collection.Extract("sng_range", out (long, long) range), Is.True);
            Assert.That(range, Is.EqualTo((3000L, 4500L)));
        }
    }

    [Test]
    public void Union_PreservesExistingNonDefaultValuesAndFillsDefaults()
    {
        var destination = new IniModifierCollection();
        AddModifier(destination, "existing_title", "Destination", ModifierType.String);
        AddModifier(destination, "existing_count", "7", ModifierType.Int32);
        AddModifier(destination, "default_enabled", "0", ModifierType.Bool);
        AddModifier(destination, "default_delay", "0", ModifierType.Int64);

        var source = new IniModifierCollection();
        AddModifier(source, "existing_title", "Source", ModifierType.String);
        AddModifier(source, "existing_count", "42", ModifierType.Int32);
        AddModifier(source, "default_enabled", "1", ModifierType.Bool);
        AddModifier(source, "default_delay", "1500", ModifierType.Int64);
        AddModifier(source, "missing_rating", "3", ModifierType.UInt32);

        destination.Union(source);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(destination.Extract("existing_title", out string title), Is.True);
            Assert.That(title, Is.EqualTo("Destination"));
            Assert.That(destination.Extract("existing_count", out int count), Is.True);
            Assert.That(count, Is.EqualTo(7));
            Assert.That(destination.Extract("default_enabled", out bool enabled), Is.True);
            Assert.That(enabled, Is.True);
            Assert.That(destination.Extract("default_delay", out long delay), Is.True);
            Assert.That(delay, Is.EqualTo(1500));
            Assert.That(destination.Extract("missing_rating", out uint rating), Is.True);
            Assert.That(rating, Is.EqualTo(3U));
        }
    }

    private static void AddModifier(IniModifierCollection collection, string output, string rawValue, ModifierType type)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawValue);
        using var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var container = new YARGTextContainer<byte>(buffer, YARGTextReader.UTF8Strict);
        YARGTextReader.SkipPureWhitespace(ref container);

        var outline = new IniModifierOutline(output, type);
        collection.Add(ref container, in outline, false);
    }

    private static void AddSngModifier(
        IniModifierCollection collection,
        string output,
        string rawValue,
        ModifierType type,
        int? length = null)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawValue);
        using var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var container = new YARGTextContainer<byte>(buffer, YARGTextReader.UTF8Strict);
        var outline = new IniModifierOutline(output, type);
        collection.AddSng(ref container, length ?? bytes.Length, in outline);
    }
}
