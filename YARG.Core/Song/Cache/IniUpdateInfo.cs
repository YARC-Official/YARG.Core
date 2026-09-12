using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    /// <summary>
    /// Everything found in a shortname's songs_updates/&lt;shortname&gt; folder that can be applied
    /// to an ini-format song: the update midi (already supported), plus the update mogg, album art,
    /// and whatever DTA-declared audio channel/panning info exists for that shortname.
    /// </summary>
    internal struct IniUpdateInfo
    {
        public string? MidiPath;
        public string? MoggPath;
        public string? ImagePath;
        public DTAEntry Dta;
    }

    /// <summary>
    /// Applies the metadata-relevant fields of a songs_updates.dta entry onto an ini song's
    /// SongMetadata. Mirrors the metadata portion of RBCONEntry.ParseDTA, but only the fields
    /// SongMetadata actually has — RBCON-only concerns (rank/intensity, hopo threshold, tuning,
    /// venue metadata, etc.) don't apply to ini songs and are left alone.
    /// </summary>
    internal static class IniDtaMetadataApplier
    {
        public static void Apply(in DTAEntry dta, ref SongMetadata metadata)
        {
            if (dta.Name != null)          { metadata.Name          = YARGDTAReader.DecodeString(dta.Name.Value, dta.MetadataEncoding); }
            if (dta.Artist != null)        { metadata.Artist        = YARGDTAReader.DecodeString(dta.Artist.Value, dta.MetadataEncoding); }
            if (dta.CoveredBy != null)     { metadata.CoveredBy     = YARGDTAReader.DecodeString(dta.CoveredBy.Value, dta.MetadataEncoding); }
            if (dta.Album != null)         { metadata.Album         = YARGDTAReader.DecodeString(dta.Album.Value, dta.MetadataEncoding); }
            if (dta.Charter != null)       { metadata.Charter       = YARGDTAReader.DecodeString(dta.Charter.Value, dta.MetadataEncoding); }
            if (dta.LoadingPhrase != null) { metadata.LoadingPhrase = YARGDTAReader.DecodeString(dta.LoadingPhrase.Value, dta.MetadataEncoding); }
            if (dta.Playlist != null)      { metadata.Playlist      = YARGDTAReader.DecodeString(dta.Playlist.Value, dta.MetadataEncoding); }

            if (dta.Genre != null)
            {
                metadata.Genre = dta.Genre;
                metadata.Subgenre = string.Empty;
            }
            if (dta.Subgenre != null) { metadata.Subgenre = dta.Subgenre.Replace("subgenre_", ""); }

            if (dta.YearAsNumber != null)          { metadata.Year          = dta.YearAsNumber.Value.ToString("D4"); }
            if (dta.YearSecondaryAsNumber != null) { metadata.YearSecondary = dta.YearSecondaryAsNumber.Value.ToString("D4"); }

            if (dta.Source != null)
            {
                // Simplified from RBCONEntry's version, which also special-cases "UGC_"-prefixed
                // node names — not meaningful for ini shortnames.
                metadata.Source = dta.Source is "ugc" or "ugc_plus" ? "customs" : dta.Source;
            }

            if (dta.SongLength != null)  { metadata.SongLength = dta.SongLength.Value; }
            if (dta.IsMaster != null)    { metadata.IsMaster    = dta.IsMaster.Value; }
            if (dta.AlbumTrack != null)  { metadata.AlbumTrack  = dta.AlbumTrack.Value; }
            if (dta.Preview != null)     { metadata.Preview     = dta.Preview.Value; }
            if (dta.SongRating != null)  { metadata.SongRating  = dta.SongRating.Value; }
            if (dta.VocalGender != null) { metadata.VocalGender = DTAEntry.ConvertVocalGender(dta.VocalGender); }
        }
    }

    /// <summary>
    /// Local (de)serialization of RBAudio&lt;T&gt; for ini cache entries — a copy of the same
    /// read/write pattern RBCONEntry uses for its own cache entries, kept local so this doesn't
    /// require touching SongEntry.RBCON.cs's private helpers.
    /// </summary>
    internal static class IniAudioSerializer
    {
        public static void WriteArray<TType>(in TType[] values, MemoryStream stream)
            where TType : unmanaged
        {
            stream.Write(values.Length, Endianness.Little);
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    var span = new ReadOnlySpan<byte>(ptr, values.Length * sizeof(TType));
                    stream.Write(span);
                }
            }
        }

        public static TType[] ReadArray<TType>(ref FixedArrayStream stream)
            where TType : unmanaged
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<TType>();
            }

            var values = new TType[length];
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    stream.Read(ptr, values.Length * sizeof(TType));
                }
            }
            return values;
        }

        public static void WriteAudio<TType>(in RBAudio<TType> audio, MemoryStream stream)
            where TType : unmanaged
        {
            WriteArray(in audio.Track, stream);
            WriteArray(in audio.Drums, stream);
            WriteArray(in audio.Bass, stream);
            WriteArray(in audio.Guitar, stream);
            WriteArray(in audio.Keys, stream);
            WriteArray(in audio.Vocals, stream);
            WriteArray(in audio.Crowd, stream);
        }

        public static void ReadAudio<TType>(ref RBAudio<TType> audio, ref FixedArrayStream stream)
            where TType : unmanaged
        {
            audio.Track  = ReadArray<TType>(ref stream);
            audio.Drums  = ReadArray<TType>(ref stream);
            audio.Bass   = ReadArray<TType>(ref stream);
            audio.Guitar = ReadArray<TType>(ref stream);
            audio.Keys   = ReadArray<TType>(ref stream);
            audio.Vocals = ReadArray<TType>(ref stream);
            audio.Crowd  = ReadArray<TType>(ref stream);
        }
    }
}
