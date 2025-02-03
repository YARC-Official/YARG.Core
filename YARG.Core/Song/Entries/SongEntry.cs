﻿using System;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Song.Cache;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    /// <summary>
    /// The type of chart file to read.
    /// </summary>
    public enum ChartFormat
    {
        Mid,
        Midi,
        Chart,
    };

    public enum EntryType
    {
        Ini,
        Sng,
        ExCON,
        CON,
    }

    public struct LoaderSettings
    {
        public static readonly LoaderSettings Default = new()
        {
            HopoThreshold = -1,
            SustainCutoffThreshold = -1,
            OverdiveMidiNote = 116
        };

        public long HopoThreshold;
        public long SustainCutoffThreshold;
        public int OverdiveMidiNote;
    }

    /// <summary>
    /// The metadata for a song.
    /// </summary>
    /// <remarks>
    /// This class is intended to hold all metadata for all songs, whether it be displayed in the song list or used for
    /// parsing/loading of the song.
    /// <br/>
    /// Display/common metadata should be added directly to this class. Metadata only used in a specific file type
    /// should not be handled through inheritance, make a separate class for that data instead and add it as a field to
    /// this one.
    /// <br/>
    /// Instances of this class should not be created directly (except for things like a chart editor), instead they
    /// should be created through static methods which parse in a metadata file of a specific type and return an
    /// instance.
    /// </remarks>
    [Serializable]
    public abstract partial class SongEntry
    {
        protected static readonly string[] BACKGROUND_FILENAMES =
        {
            "bg", "background", "video"
        };

        protected static readonly string[] VIDEO_EXTENSIONS =
        {
            ".mp4", ".mov", ".webm",
        };

        protected static readonly string[] IMAGE_EXTENSIONS =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".pic"
        };

        protected static readonly string YARGROUND_EXTENSION = ".yarground";
        protected static readonly string YARGROUND_FULLNAME = "bg.yarground";
        protected static readonly Random BACKROUND_RNG = new();

        private SortString _name = SortString.Empty;
        private SortString _artist = SortString.Empty;
        private SortString _album = SortString.Empty;
        private SortString _genre = SortString.Empty;
        private SortString _charter = SortString.Empty;
        private SortString _source = SortString.Empty;
        private SortString _playlist = SortString.Empty;
        private bool _isDuplicate = false;

        protected SongMetadata _metadata = SongMetadata.Default;
        protected AvailableParts _parts = AvailableParts.Default;
        protected HashWrapper _hash = default;
        protected LoaderSettings _settings = LoaderSettings.Default;
        protected string _parsedYear = string.Empty;
        protected int _yearAsNumber = int.MaxValue;

        public abstract EntryType SubType { get; }
        public abstract string SortBasedLocation { get; }
        public abstract string ActualLocation { get; }

        public HashWrapper Hash => _hash;
        public SortString Name => _name;
        public SortString Artist => _artist;
        public SortString Album => _album;
        public SortString Genre => _genre;
        public SortString Charter => _charter;
        public SortString Source => _source;
        public SortString Playlist => _playlist;

        public string UnmodifiedYear => _metadata.Year;
        public string ParsedYear => _parsedYear;
        public int YearAsNumber => _yearAsNumber;

        public bool IsMaster => _metadata.IsMaster;
        public bool VideoLoop => _metadata.VideoLoop;

        public int AlbumTrack => _metadata.AlbumTrack;

        public int PlaylistTrack => _metadata.PlaylistTrack;

        public string LoadingPhrase => _metadata.LoadingPhrase;
        
        public string CreditWrittenBy => _metadata.CreditWrittenBy;
        
        public string CreditPerformedBy => _metadata.CreditPerformedBy;
        
        public string CreditCourtesyOf => _metadata.CreditCourtesyOf;
        
        public string CreditAlbumCover => _metadata.CreditAlbumCover;
        
        public string CreditLicense => _metadata.CreditLicense;

        public long SongLengthMilliseconds => _metadata.SongLength;

        public long SongOffsetMilliseconds => _metadata.SongOffset;

        public long PreviewStartMilliseconds => _metadata.Preview.Start;

        public long PreviewEndMilliseconds => _metadata.Preview.End;

        public long VideoStartTimeMilliseconds => _metadata.Video.Start;

        public long VideoEndTimeMilliseconds => _metadata.Video.End;

        public double SongLengthSeconds => SongLengthMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double SongOffsetSeconds => SongOffsetMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double PreviewStartSeconds => PreviewStartMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double PreviewEndSeconds => PreviewEndMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double VideoStartTimeSeconds => VideoStartTimeMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double VideoEndTimeSeconds => VideoEndTimeMilliseconds >= 0 ? VideoEndTimeMilliseconds / SongMetadata.MILLISECOND_FACTOR : -1;

        public int VocalsCount
        {
            get
            {
                if (_parts.HarmonyVocals[2])
                {
                    return 3;
                }

                if (_parts.HarmonyVocals[1])
                {
                    return 2;
                }
                return _parts.HarmonyVocals[0] || _parts.LeadVocals[0] ? 1 : 0;
            }
        }

        public sbyte BandDifficulty => _parts.BandDifficulty.Intensity;

        public bool IsDuplicate => _isDuplicate;

        public abstract DateTime GetLastWriteTime();

        public override string ToString() { return Artist + " | " + Name; }

        public PartValues this[Instrument instrument]
        {
            get
            {
                return instrument switch
                {
                    Instrument.FiveFretGuitar => _parts.FiveFretGuitar,
                    Instrument.FiveFretBass => _parts.FiveFretBass,
                    Instrument.FiveFretRhythm => _parts.FiveFretRhythm,
                    Instrument.FiveFretCoopGuitar => _parts.FiveFretCoopGuitar,
                    Instrument.Keys => _parts.Keys,

                    Instrument.SixFretGuitar => _parts.SixFretGuitar,
                    Instrument.SixFretBass => _parts.SixFretBass,
                    Instrument.SixFretRhythm => _parts.SixFretRhythm,
                    Instrument.SixFretCoopGuitar => _parts.SixFretCoopGuitar,

                    Instrument.FourLaneDrums => _parts.FourLaneDrums,
                    Instrument.FiveLaneDrums => _parts.FiveLaneDrums,
                    Instrument.ProDrums => _parts.ProDrums,

                    Instrument.EliteDrums => _parts.EliteDrums,

                    Instrument.ProGuitar_17Fret => _parts.ProGuitar_17Fret,
                    Instrument.ProGuitar_22Fret => _parts.ProGuitar_22Fret,
                    Instrument.ProBass_17Fret => _parts.ProBass_17Fret,
                    Instrument.ProBass_22Fret => _parts.ProBass_22Fret,

                    Instrument.ProKeys => _parts.ProKeys,

                    // Instrument.Dj => DJ,

                    Instrument.Vocals => _parts.LeadVocals,
                    Instrument.Harmony => _parts.HarmonyVocals,
                    Instrument.Band => _parts.BandDifficulty,

                    _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
                };
            }
        }

        public bool HasInstrument(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => _parts.FiveFretGuitar.IsActive(),
                Instrument.FiveFretBass => _parts.FiveFretBass.IsActive(),
                Instrument.FiveFretRhythm => _parts.FiveFretRhythm.IsActive(),
                Instrument.FiveFretCoopGuitar => _parts.FiveFretCoopGuitar.IsActive(),
                Instrument.Keys => _parts.Keys.IsActive(),

                Instrument.SixFretGuitar => _parts.SixFretGuitar.IsActive(),
                Instrument.SixFretBass => _parts.SixFretBass.IsActive(),
                Instrument.SixFretRhythm => _parts.SixFretRhythm.IsActive(),
                Instrument.SixFretCoopGuitar => _parts.SixFretCoopGuitar.IsActive(),

                Instrument.FourLaneDrums => _parts.FourLaneDrums.IsActive(),
                Instrument.FiveLaneDrums => _parts.FiveLaneDrums.IsActive(),
                Instrument.ProDrums => _parts.ProDrums.IsActive(),

                Instrument.EliteDrums => _parts.EliteDrums.IsActive(),

                Instrument.ProGuitar_17Fret => _parts.ProGuitar_17Fret.IsActive(),
                Instrument.ProGuitar_22Fret => _parts.ProGuitar_22Fret.IsActive(),
                Instrument.ProBass_17Fret => _parts.ProBass_17Fret.IsActive(),
                Instrument.ProBass_22Fret => _parts.ProBass_22Fret.IsActive(),

                Instrument.ProKeys => _parts.ProKeys.IsActive(),

                Instrument.Vocals => _parts.LeadVocals.IsActive(),
                Instrument.Harmony => _parts.HarmonyVocals.IsActive(),
                Instrument.Band => _parts.BandDifficulty.IsActive(),

                _ => false
            };
        }

        internal void MarkAsDuplicate() { _isDuplicate = true; }

        internal virtual void Serialize(MemoryStream stream, CacheWriteIndices node)
        {
            _hash.Serialize(stream);
            unsafe
            {
                var parts = _parts;
                stream.Write(new Span<byte>(&parts, sizeof(AvailableParts)));
            }

            stream.Write(node.Title, Endianness.Little);
            stream.Write(node.Artist, Endianness.Little);
            stream.Write(node.Album, Endianness.Little);
            stream.Write(node.Genre, Endianness.Little);
            stream.Write(node.Year, Endianness.Little);
            stream.Write(node.Charter, Endianness.Little);
            stream.Write(node.Playlist, Endianness.Little);
            stream.Write(node.Source, Endianness.Little);

            stream.Write(_metadata.IsMaster);
            stream.Write(_metadata.VideoLoop);

            stream.Write(_metadata.AlbumTrack, Endianness.Little);
            stream.Write(_metadata.PlaylistTrack, Endianness.Little);

            stream.Write(_metadata.SongLength, Endianness.Little);
            stream.Write(_metadata.SongOffset, Endianness.Little);
            stream.Write((int)_metadata.SongRating, Endianness.Little);

            stream.Write(_metadata.Preview.Start, Endianness.Little);
            stream.Write(_metadata.Preview.End, Endianness.Little);

            stream.Write(_metadata.Video.Start, Endianness.Little);
            stream.Write(_metadata.Video.End, Endianness.Little);

            stream.Write(_metadata.LoadingPhrase);

            stream.Write(_metadata.CreditWrittenBy);
            stream.Write(_metadata.CreditPerformedBy);
            stream.Write(_metadata.CreditCourtesyOf);
            stream.Write(_metadata.CreditAlbumCover);
            stream.Write(_metadata.CreditLicense);

            stream.Write(_settings.HopoThreshold, Endianness.Little);
            stream.Write(_settings.SustainCutoffThreshold, Endianness.Little);
            stream.Write(_settings.OverdiveMidiNote, Endianness.Little);
        }

        protected SongEntry() { }

        private protected void Deserialize(ref FixedArrayStream stream, CacheReadStrings strings)
        {
            _hash = HashWrapper.Deserialize(ref stream);
            unsafe
            {
                AvailableParts parts;
                stream.Read(&parts, sizeof(AvailableParts));
                _parts = parts;
            }

            _metadata.Name =     strings.Titles   [stream.Read<int>(Endianness.Little)];
            _metadata.Artist =   strings.Artists  [stream.Read<int>(Endianness.Little)];
            _metadata.Album =    strings.Albums   [stream.Read<int>(Endianness.Little)];
            _metadata.Genre =    strings.Genres   [stream.Read<int>(Endianness.Little)];
            _metadata.Year =     strings.Years    [stream.Read<int>(Endianness.Little)];
            _metadata.Charter =  strings.Charters [stream.Read<int>(Endianness.Little)];
            _metadata.Playlist = strings.Playlists[stream.Read<int>(Endianness.Little)];
            _metadata.Source =   strings.Sources  [stream.Read<int>(Endianness.Little)];

            _metadata.IsMaster =  stream.ReadBoolean();
            _metadata.VideoLoop = stream.ReadBoolean();

            _metadata.AlbumTrack =    stream.Read<int>(Endianness.Little);
            _metadata.PlaylistTrack = stream.Read<int>(Endianness.Little);

            _metadata.SongLength = stream.Read<long>(Endianness.Little);
            _metadata.SongOffset = stream.Read<long>(Endianness.Little);
            _metadata.SongRating = (SongRating)stream.Read<uint>(Endianness.Little);

            _metadata.Preview.Start = stream.Read<long>(Endianness.Little);
            _metadata.Preview.End   = stream.Read<long>(Endianness.Little);

            _metadata.Video.Start = stream.Read<long>(Endianness.Little);
            _metadata.Video.End = stream.Read<long>(Endianness.Little);

            _metadata.LoadingPhrase = stream.ReadString();

            _metadata.CreditWrittenBy = stream.ReadString();
            _metadata.CreditPerformedBy = stream.ReadString();
            _metadata.CreditCourtesyOf = stream.ReadString();
            _metadata.CreditAlbumCover = stream.ReadString();
            _metadata.CreditLicense = stream.ReadString();

            _settings.HopoThreshold = stream.Read<long>(Endianness.Little);
            _settings.SustainCutoffThreshold = stream.Read<long>(Endianness.Little);
            _settings.OverdiveMidiNote = stream.Read<int>(Endianness.Little);

            SetSortStrings();
        }

        protected void SetSortStrings()
        {
            _name = new SortString(_metadata.Name);
            _artist = new SortString(_metadata.Artist);
            _album = new SortString(_metadata.Album);
            _genre = new SortString(_metadata.Genre);
            _charter = new SortString(_metadata.Charter);
            _source = new SortString(_metadata.Source);
            _playlist = new SortString(_metadata.Playlist);
        }
    }
}