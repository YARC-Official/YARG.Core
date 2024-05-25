using Cysharp.Text;
using System;
using System.Globalization;
using System.Text;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    public readonly struct SortString : IComparable<SortString>, IEquatable<SortString>
    {
        // Order of these static variables matters
        private static readonly (string, string)[] SearchLeniency =
        {
            ("Æ", "AE") // Tool - Ænema
        };

        public static readonly SortString Empty = new(string.Empty);

        public readonly string Str;
        public readonly string SortStr;
        public readonly int HashCode;

        public int Length => Str.Length;

        public SortString(string str)
        {
            Str = str;
            SortStr = RemoveDiacritics(RichTextUtils.StripRichTextTags(str));
            HashCode = SortStr.GetHashCode();
        }

        public int CompareTo(SortString other)
        {
            return SortStr.CompareTo(other.SortStr);
        }

        public override int GetHashCode()
        {
            return HashCode;
        }

        public bool Equals(SortString other)
        {
            return SortStr.Equals(other.SortStr);
        }

        public override string ToString()
        {
            return Str;
        }

        public static implicit operator SortString(string str) => new(str);
        public static implicit operator string(SortString str) => str.Str;

        public static string RemoveDiacritics(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            foreach (var c in SearchLeniency)
            {
                text = text.Replace(c.Item1, c.Item2);
            }

            var normalizedString = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            using var stringBuilder = ZString.CreateStringBuilder();
            stringBuilder.TryGrow(normalizedString.Length);
            foreach (char c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }
    }
}
