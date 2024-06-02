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
            SortStr = RemoveUnwantedWhitespace(RemoveDiacritics(RichTextUtils.StripRichTextTags(str)));
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

        public static unsafe string RemoveUnwantedWhitespace(string arg)
        {
            var buffer = stackalloc char[arg.Length];
            int length = 0;
            int index = 0;
            while (index < arg.Length)
            {
                char curr = arg[index++];
                if (curr > 32 || (length > 0 && buffer[length - 1] > 32))
                {
                    if (curr > 32)
                    {
                        buffer[length++] = curr;
                    }
                    else
                    {
                        while (index < arg.Length && arg[index] <= 32)
                        {
                            index++;
                        }

                        if (index == arg.Length)
                        {
                            break;
                        }

                        buffer[length++] = ' ';
                    }
                }
            }
            return length == arg.Length ? arg : new string(buffer, 0, length);
        }
    }
}
