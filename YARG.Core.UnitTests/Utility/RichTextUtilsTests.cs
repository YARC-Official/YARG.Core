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
    }
}