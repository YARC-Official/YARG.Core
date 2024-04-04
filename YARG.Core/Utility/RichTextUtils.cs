using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Utility
{
    [Flags]
    public enum RichTextTags : ulong
    {
        None = 0,

        // Organized according to the text tags in alphabetical order
        /// <summary>The "align" tag.</summary>
        Align = 1UL << 0,
        /// <summary>The "allcaps" tag.</summary>
        AllCaps = 1UL << 1,
        /// <summary>The "alpha" tag.</summary>
        Alpha = 1UL << 2,
        /// <summary>The "b" tag.</summary>
        Bold = 1UL << 3,
        /// <summary>The "br" tag.</summary>
        LineBreak = 1UL << 4,
        /// <summary>The "color" tag.</summary>
        Color = 1UL << 5,
        /// <summary>The "cspace" tag.</summary>
        CharSpace = 1UL << 6,
        /// <summary>The "font" tag.</summary>
        Font = 1UL << 7,
        /// <summary>The "font-weight" tag.</summary>
        FontWeight = 1UL << 8,
        /// <summary>The "gradient" tag.</summary>
        Gradient = 1UL << 9,
        /// <summary>The "i" tag.</summary>
        Italics = 1UL << 10,
        /// <summary>The "indent" tag.</summary>
        Indent = 1UL << 11,
        /// <summary>The "line-height" tag.</summary>
        LineHeight = 1UL << 12,
        /// <summary>The "line-indent" tag.</summary>
        LineIndent = 1UL << 13,
        /// <summary>The "link" tag.</summary>
        Link = 1UL << 14,
        /// <summary>The "lowercase" tag.</summary>
        Lowercase = 1UL << 15,
        /// <summary>The "margin" tag.</summary>
        Margin = 1UL << 16,
        /// <summary>The "mark" tag.</summary>
        Mark = 1UL << 17,
        /// <summary>The "mspace" tag.</summary>
        Monospace = 1UL << 18,
        /// <summary>The "noparse" tag.</summary>
        NoParse = 1UL << 19,
        /// <summary>The "nobr" tag.</summary>
        NoBreak = 1UL << 20,
        /// <summary>The "page" tag.</summary>
        PageBreak = 1UL << 21,
        /// <summary>The "pos" tag.</summary>
        HorizontalPosition = 1UL << 22,
        /// <summary>The "rotate" tag.</summary>
        Rotate = 1UL << 23,
        /// <summary>The "size" tag.</summary>
        FontSize = 1UL << 24,
        /// <summary>The "smallcaps" tag.</summary>
        SmallCaps = 1UL << 25,
        /// <summary>The "space" tag.</summary>
        HorizontalSpace = 1UL << 26,
        /// <summary>The "sprite" tag.</summary>
        Sprite = 1UL << 27,
        /// <summary>The "s" tag.</summary>
        Strikethrough = 1UL << 28,
        /// <summary>The "style" tag.</summary>
        Style = 1UL << 29,
        /// <summary>The "sub" tag.</summary>
        Subscript = 1UL << 30,
        /// <summary>The "sup" tag.</summary>
        Superscript = 1UL << 31,
        /// <summary>The "u" tag.</summary>
        Underline = 1UL << 32,
        /// <summary>The "uppercase" tag.</summary>
        Uppercase = 1UL << 33,
        /// <summary>The "voffset" tag.</summary>
        VerticalOffset = 1UL << 34,
        /// <summary>The "width" tag.</summary>
        Width = 1UL << 35,

        MaxBit = Width,

        AllTags = ~0UL,

        /// <summary>Tags which are acceptable for general purposes.</summary>
        GoodTags = Alpha | Color | Bold | Italics | Lowercase | Uppercase |
            SmallCaps | Strikethrough | Underline | Subscript | Superscript,

        /// <summary>Tags which are not desirable for general purposes.</summary>
        BadTags = ~GoodTags,
    }

    public static class RichTextUtils
    {
        internal static readonly string[] RICH_TEXT_TAGS =
        {
            "align", "allcaps", "alpha", "b", "br", "color", "cspace", "font", "font-weight", "gradient", "i",
            "indent", "line-height", "line-indent", "link", "lowercase", "margin", "mark", "mspace", "noparse",
            "nobr", "page", "pos", "rotate", "size", "smallcaps", "space", "sprite", "s", "style", "sub", "sup",
            "u", "uppercase", "voffset", "width",
        };

        internal static Dictionary<string, string> COLOR_TO_HEX = new()
        {
            { "aqua",      "#00ffff" },
            { "black",     "#000000" },
            { "blue",      "#0000ff" },
            { "brown",     "#a52a2a" },
            { "cyan",      "#00ffff" },
            { "darkblue",  "#0000a0" },
            { "fuchsia",   "#ff00ff" },
            { "green",     "#008000" },
            { "grey",      "#808080" },
            { "lightblue", "#add8e6" },
            { "lime",      "#00ff00" },
            { "magenta",   "#ff00ff" },
            { "maroon",    "#800000" },
            { "navy",      "#000080" },
            { "olive",     "#808000" },
            { "orange",    "#ffa500" },
            { "purple",    "#800080" },
            { "red",       "#ff0000" },
            { "silver",    "#c0c0c0" },
            { "teal",      "#008080" },
            { "white",     "#ffffff" },
            { "yellow",    "#ffff00" },
        };

        private static readonly ConcurrentDictionary<RichTextTags, Regex> REGEX_CACHE = new();

        public static string StripRichTextTags(string text)
        {
            using var builder = ZString.CreateStringBuilder(notNested: true);

            var textSpan = text.AsSpan();
            bool tagsFound = false;
            while (FindNextTag(textSpan, out var beforeTag, out _, out var remaining))
            {
                textSpan = remaining;
                tagsFound = true;
                builder.Append(beforeTag);
            }

            // Return unmodified if no modifications were made
            if (!tagsFound)
                return text;

            builder.Append(textSpan);
            return builder.ToString();
        }

        public static string StripRichTextTags(string text, RichTextTags excludeTags)
        {
            if (!REGEX_CACHE.TryGetValue(excludeTags, out var regex))
                regex = ConstructRegex(excludeTags);
            return regex.Replace(text, "");
        }

        public static string StripRichTextTagsExcept(string text, RichTextTags keepTags)
        {
            return StripRichTextTags(text, ~keepTags);
        }

        private static Regex ConstructRegex(RichTextTags tags)
        {
            string regexFormat = @"<\/*{0}.*?>|";

            var sb = new StringBuilder();
            ulong bit;
            for (int i = 0; i < sizeof(ulong) * 8 && (bit = 1UL << i) <= (ulong) RichTextTags.MaxBit; i++)
            {
                if ((tags & (RichTextTags) bit) != 0)
                {
                    sb.AppendFormat(regexFormat, RICH_TEXT_TAGS[i]);
                }
            }

            if (sb.Length > 0) regexFormat = sb.Remove(sb.Length - 1, 1).ToString();

            var regex = new Regex(regexFormat, RegexOptions.Compiled);
            REGEX_CACHE[tags] = regex;
            return regex;
        }

        public static string ReplaceColorNames(string text)
        {
            using var builder = ZString.CreateStringBuilder(notNested: true);

            var textSpan = text.AsSpan();
            bool tagsFound = false;
            while (FindNextTag(textSpan, out var beforeTag, out var tag, out var remaining))
            {
                textSpan = remaining;
                tagsFound = true;
                builder.Append(beforeTag);

                const string colorTag = "<color=";
                if (tag.StartsWith(colorTag))
                {
                    var name = tag[colorTag.Length..].TrimEndOnce('>').TrimOnce('"');

                    // Allocation is taken because it is significantly faster to use
                    // a Dictionary than it is to manually enumerate each possible value
                    if (COLOR_TO_HEX.TryGetValue(name.ToString(), out string hexCode))
                    {
                        builder.Append(colorTag);
                        builder.Append(hexCode);
                        builder.Append('>');
                    }
                }
                else
                {
                    builder.Append(tag);
                }
            }

            // Return unmodified if no modifications were made
            if (!tagsFound)
                return text;

            builder.Append(textSpan);
            return builder.ToString();
        }

        private static bool FindNextTag(ReadOnlySpan<char> text,
            out ReadOnlySpan<char> beforeTag, out ReadOnlySpan<char> tag, out ReadOnlySpan<char> remaining)
        {
            beforeTag = ReadOnlySpan<char>.Empty;
            tag = ReadOnlySpan<char>.Empty;
            remaining = ReadOnlySpan<char>.Empty;

            int tagIndex = text.IndexOf('<');
            if (tagIndex < 0)
                return false;

            beforeTag = text[..tagIndex];
            tag = text[tagIndex..];

            int tagCloseIndex = tag.IndexOf('>');
            if (tagCloseIndex < 0)
                return false;

            remaining = tag[++tagCloseIndex..];
            tag = tag[..tagCloseIndex];
            return true;
        }
    }
}