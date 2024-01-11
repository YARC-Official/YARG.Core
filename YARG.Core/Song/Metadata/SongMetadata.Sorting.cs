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
        Instrument,
    };

    public sealed partial class SongMetadata : IComparable<SongMetadata>
    {
        public int CompareTo(SongMetadata other)
        {
            int strCmp;
            if ((strCmp = Name.CompareTo(other.Name)) == 0 &&
                (strCmp = Artist.CompareTo(other.Artist)) == 0 &&
                (strCmp = Album.CompareTo(other.Album)) == 0 &&
                (strCmp = Charter.CompareTo(other.Charter)) == 0)
            {
                strCmp = Directory.CompareTo(other.Directory);
            }
            return strCmp;
        }

        private enum EntryType
        {
            Ini,
            Sng,
            ExCON,
            CON,
        }

        public bool IsPreferedOver(SongMetadata other)
        {
            static EntryType ParseType(SongMetadata entry)
            {
                if (entry._iniData != null)
                {
                    return entry._iniData is SngSubmetadata ? EntryType.Sng : EntryType.Ini;
                }

                return entry._rbData is RBPackedCONMetadata ? EntryType.CON : EntryType.ExCON;
            }

            var thisType = ParseType(this);
            var otherType = ParseType(other);
            if (thisType != otherType)
            {
                // CON > ExCON > Sng > Ini
                return thisType > otherType;
            }
            // Otherwise, whatever would appear first
            return CompareTo(other) < 0;
        }

    }

    public sealed class EntryComparer : IComparer<SongMetadata>
    {
        private readonly SongAttribute attribute;

        public EntryComparer(SongAttribute attribute) { this.attribute = attribute; }

        public int Compare(SongMetadata lhs, SongMetadata rhs) { return IsLowerOrdered(lhs, rhs) ? -1 : 1; }

        private bool IsLowerOrdered(SongMetadata lhs, SongMetadata rhs)
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
                    if (lhs.SongLengthMilliseconds != rhs.SongLengthMilliseconds)
                        return lhs.SongLengthMilliseconds < rhs.SongLengthMilliseconds;
                    break;
            }

            return lhs.CompareTo(rhs) < 0;
        }
    }

    public sealed class InstrumentComparer : IComparer<SongMetadata>
    {
        private static readonly EntryComparer baseComparer = new(SongAttribute.Unspecified);
        public readonly Instrument instrument;
        public InstrumentComparer(Instrument instrument)
        {
            this.instrument = instrument;
        }

        public int Compare(SongMetadata lhs, SongMetadata rhs)
        {
            var lhsValues = lhs.Parts.GetValues(instrument);
            var rhsValues = rhs.Parts.GetValues(instrument);

            // This function only gets called if both entries have the instrument
            // That check is not necessary
            if (lhsValues.intensity != rhsValues.intensity)
            {
                if (lhsValues.intensity != -1 && (rhsValues.intensity == -1 || lhsValues.intensity < rhsValues.intensity))
                    return -1;
                return 1;
            }
            
            if (lhsValues.subTracks > rhsValues.subTracks)
                return -1;

            if (lhsValues.subTracks < rhsValues.subTracks)
                return 1;

            return baseComparer.Compare(lhs, rhs);
        }
    }
}
