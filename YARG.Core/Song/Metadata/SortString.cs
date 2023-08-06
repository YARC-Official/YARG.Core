using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YARG.Core.Song
{
    public struct SortString : IComparable<SortString>, IEquatable<SortString>
    {
        private string _str;
        private string _sortStr;
        private int _hashCode;

        public string Str
        {
            get { return _str; }
            set
            {
                _str = value;
                _sortStr = RemoveDiacritics(value);
                _hashCode = _sortStr.GetHashCode();
            }
        }

        public int Length => _str.Length;

        public string SortStr => _sortStr;

        public SortString(string str)
        {
            _sortStr = _str = string.Empty;
            _hashCode = 0;
            Str = str;
        }

        public int CompareTo(SortString other)
        {
            return _sortStr.CompareTo(other._sortStr);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public bool Equals(SortString other)
        {
            return _sortStr.Equals(other._sortStr);
        }

        public override string ToString()
        {
            return _str;
        }

        public static implicit operator SortString(string str) => new(str);
        public static implicit operator string(SortString str) => str.Str;

        private static readonly List<(string, string)> SearchLeniency = new()
        {
            ("Æ", "AE") // Tool - Ænema
        };

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
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

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
