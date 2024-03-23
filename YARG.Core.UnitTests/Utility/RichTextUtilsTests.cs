using NUnit.Framework;
using YARG.Core.Utility;

namespace YARG.Core.UnitTests.Utility
{
    public class RichTextUtilsTests
    {
        private static readonly List<(string, RichTextTags)> TEXT_TO_TAG =
        [
            ("align",       RichTextTags.Align),
            ("allcaps",     RichTextTags.AllCaps),
            ("alpha",       RichTextTags.Alpha),
            ("b",           RichTextTags.Bold),
            ("br",          RichTextTags.LineBreak),
            ("color",       RichTextTags.Color),
            ("cspace",      RichTextTags.CharSpace),
            ("font",        RichTextTags.Font),
            ("font-weight", RichTextTags.FontWeight),
            ("gradient",    RichTextTags.Gradient),
            ("i",           RichTextTags.Italics),
            ("indent",      RichTextTags.Indent),
            ("line-height", RichTextTags.LineHeight),
            ("line-indent", RichTextTags.LineIndent),
            ("link",        RichTextTags.Link),
            ("lowercase",   RichTextTags.Lowercase),
            ("margin",      RichTextTags.Margin),
            ("mark",        RichTextTags.Mark),
            ("mspace",      RichTextTags.Monospace),
            ("noparse",     RichTextTags.NoParse),
            ("nobr",        RichTextTags.NoBreak),
            ("page",        RichTextTags.PageBreak),
            ("pos",         RichTextTags.HorizontalPosition),
            ("rotate",      RichTextTags.Rotate),
            ("size",        RichTextTags.FontSize),
            ("smallcaps",   RichTextTags.SmallCaps),
            ("space",       RichTextTags.HorizontalSpace),
            ("sprite",      RichTextTags.Sprite),
            ("s",           RichTextTags.Strikethrough),
            ("style",       RichTextTags.Style),
            ("sub",         RichTextTags.Subscript),
            ("sup",         RichTextTags.Superscript),
            ("u",           RichTextTags.Underline),
            ("uppercase",   RichTextTags.Uppercase),
            ("voffset",     RichTextTags.VerticalOffset),
            ("width",       RichTextTags.Width),
        ];

        internal static List<(string name, string hex)> COLOR_NAMES =
        [
            ("aqua",      "#00ffff"),
            ("black",     "#000000"),
            ("blue",      "#0000ff"),
            ("brown",     "#a52a2a"),
            ("cyan",      "#00ffff"),
            ("darkblue",  "#0000a0"),
            ("fuchsia",   "#ff00ff"),
            ("green",     "#008000"),
            ("grey",      "#808080"),
            ("lightblue", "#add8e6"),
            ("lime",      "#00ff00"),
            ("magenta",   "#ff00ff"),
            ("maroon",    "#800000"),
            ("navy",      "#000080"),
            ("olive",     "#808000"),
            ("orange",    "#ffa500"),
            ("purple",    "#800080"),
            ("red",       "#ff0000"),
            ("silver",    "#c0c0c0"),
            ("teal",      "#008080"),
            ("white",     "#ffffff"),
            ("yellow",    "#ffff00"),
        ];

        [TestCase]
        public void ReplacesTags()
        {
            Assert.Multiple(() =>
            {
                foreach (var (tagText, tag) in TEXT_TO_TAG)
                {
                    const string expectedText = "Some formatting";
                    string testText = $"Some <{tagText}=50vb>formatting</{tagText}>";

                    string stripped = RichTextUtils.StripRichTextTags(testText, tag);
                    Assert.That(stripped, Is.EqualTo(expectedText), $"Tag '{tagText}' was not stripped!");
                }
            });
        }

        [TestCase]
        public void ReplacesColors()
        {
            Assert.Multiple(() =>
            {
                foreach (var (name, hex) in COLOR_NAMES)
                {
                    string expectedText = $"Some <color={hex}>formatting</color>";
                    string testText = $"Some <color={name}>formatting</color>";

                    string stripped = RichTextUtils.ReplaceColorNames(testText);
                    Assert.That(stripped, Is.EqualTo(expectedText), $"Color name '{name}' was not replaced!");
                }
            });
        }
    }
}