using System;
using System.Collections.Generic;

namespace YARG.Core.Song
{
    public enum SongAttribute
    {
        Unspecified,
        Name,
        Artist,
        Album,
        Artist_Album,
        Genre,
        Year,
        Charter,
        Playlist,
        Source,
        SongLength,
    };

    public sealed partial class SongMetadata
    {
        public string GetStringAttribute(SongAttribute attribute)
        {
            return attribute switch
            {
                SongAttribute.Name => _name.Str,
                SongAttribute.Artist => _artist.Str,
                SongAttribute.Album => _album.Str,
                SongAttribute.Genre => _genre.Str,
                SongAttribute.Year => _unmodifiedYear,
                SongAttribute.Charter => _charter.Str,
                SongAttribute.Playlist => _playlist.Str,
                SongAttribute.Source => _source.Str,
                _ => throw new Exception("stoopid - only string attributes can be used here"),
            };
        }
    }

    public class EntryComparer : IComparer<SongMetadata>
    {
        private readonly SongAttribute attribute;

        public EntryComparer(SongAttribute attribute) { this.attribute = attribute; }

        public int Compare(SongMetadata lhs, SongMetadata rhs) { return IsLowerOrdered(lhs, rhs) ? -1 : 1; }

        protected bool IsLowerOrdered(SongMetadata lhs, SongMetadata rhs)
        {
            switch (attribute)
            {
                case SongAttribute.Album:
                    if (lhs.AlbumTrack != rhs.AlbumTrack)
                        return lhs.AlbumTrack < rhs.AlbumTrack;
                    break;
                case SongAttribute.Year:
                    if (lhs.YearAsNumber != rhs.YearAsNumber)
                        return lhs.YearAsNumber < rhs.YearAsNumber;
                    break;
                case SongAttribute.Playlist:
                    if (lhs.RBData != null && rhs.RBData != null)
                    {
                        int lhsBand = lhs.RBData.SharedMetadata.RBDifficulties.band;
                        int rhsBand = rhs.RBData.SharedMetadata.RBDifficulties.band;
                        if (lhsBand != -1 && rhsBand != -1)
                            return lhsBand < rhsBand;
                    }

                    if (lhs.PlaylistTrack != rhs.PlaylistTrack)
                        return lhs.PlaylistTrack < rhs.PlaylistTrack;

                    if (lhs.Parts.BandDifficulty != rhs.Parts.BandDifficulty)
                        return lhs.Parts.BandDifficulty < rhs.Parts.BandDifficulty;
                    break;
                case SongAttribute.SongLength:
                    if (lhs.SongLength != rhs.SongLength)
                        return lhs.SongLength < rhs.SongLength;
                    break;
            }

            int strCmp;
            if ((strCmp = lhs.Name.CompareTo(rhs.Name)) != 0 ||
                (strCmp = lhs.Artist.CompareTo(rhs.Artist)) != 0 ||
                (strCmp = lhs.Album.CompareTo(rhs.Album)) != 0 ||
                (strCmp = lhs.Charter.CompareTo(rhs.Charter)) != 0)
                return strCmp < 0;
            else
                return lhs.Directory.CompareTo(rhs.Directory) < 0;
        }
    }
}
