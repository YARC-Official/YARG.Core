using System;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    public readonly struct SortString : IComparable<SortString>
    {
        public static readonly SortString Empty = new(string.Empty);

        private readonly string _original;
        private readonly string _searchStr;
        private readonly string _sortStr;
        private readonly CharacterGroup _group;
        private readonly int _hashcode;

        public string Original => _original;
        public string SearchStr => _searchStr;
        public string SortStr => _sortStr;
        public CharacterGroup Group => _group;
        public int Length => Original.Length;
        public char this[int index] => Original[index];

        public SortString(string str)
        {
            _original = str;
            _searchStr = StringTransformations.RemoveUnwantedWhitespace(StringTransformations.RemoveDiacritics(RichTextUtils.StripRichTextTags(str)));
            _sortStr = StringTransformations.RemoveArticle(_searchStr);
            _group = StringTransformations.GetCharacterGrouping(_sortStr);
            _hashcode = _sortStr.GetHashCode();
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override string ToString()
        {
            return _original.ToString();
        }

        public int CompareTo(SortString other)
        {
            if (_group != other._group)
            {
                return _group - other._group;
            }
            return string.CompareOrdinal(_sortStr, other._sortStr);
        }

        public static implicit operator string(in SortString str) => str.Original;
    }
}
