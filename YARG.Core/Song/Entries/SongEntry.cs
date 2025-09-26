using System;
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

        protected const string YARGROUND_EXTENSION = ".yarground";
        protected const string YARGROUND_FULLNAME = "bg.yarground";
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

        public SongRating SongRating => _metadata.SongRating;

        public string LoadingPhrase => _metadata.LoadingPhrase;

        public string LinkBandcamp  => _metadata.LinkBandcamp;
        public string LinkBluesky   => _metadata.LinkBluesky;
        public string LinkFacebook  => _metadata.LinkFacebook;
        public string LinkInstagram => _metadata.LinkInstagram;
        public string LinkSpotify   => _metadata.LinkSpotify;
        public string LinkTwitter   => _metadata.LinkTwitter;
        public string LinkOther     => _metadata.LinkOther;
        public string LinkYoutube   => _metadata.LinkYoutube;

        public string Location      => _metadata.Location;

        public string CreditAlbumArtDesignedBy   => _metadata.CreditAlbumArtDesignedBy;
        public string CreditArrangedBy           => _metadata.CreditArrangedBy;
        public string CreditComposedBy           => _metadata.CreditComposedBy;
        public string CreditCourtesyOf           => _metadata.CreditCourtesyOf;
        public string CreditEngineeredBy         => _metadata.CreditEngineeredBy;
        public string CreditLicense              => _metadata.CreditLicense;
        public string CreditMasteredBy           => _metadata.CreditMasteredBy;
        public string CreditMixedBy              => _metadata.CreditMixedBy;
        public string CreditOther                => _metadata.CreditOther;
        public string CreditPerformedBy          => _metadata.CreditPerformedBy;
        public string CreditProducedBy           => _metadata.CreditProducedBy;
        public string CreditPublishedBy          => _metadata.CreditPublishedBy;
        public string CreditWrittenBy            => _metadata.CreditWrittenBy;

        public string CharterBass       => _metadata.CharterBass;
        public string CharterDrums      => _metadata.CharterDrums;
        public string CharterEliteDrums => _metadata.CharterEliteDrums;
        public string CharterGuitar     => _metadata.CharterGuitar;
        public string CharterKeys       => _metadata.CharterKeys;
        public string CharterLowerDiff  => _metadata.CharterLowerDiff;
        public string CharterProBass    => _metadata.CharterProBass;
        public string CharterProKeys    => _metadata.CharterProKeys;
        public string CharterProGuitar  => _metadata.CharterProGuitar;
        public string CharterVocals     => _metadata.CharterVocals;
        public string CharterVenue      => _metadata.CharterVenue;

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

        public float? VocalScrollSpeedScalingFactor => _metadata.VocalScrollSpeedScalingFactor;

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

        /// <summary>
        /// Checks that song has the given difficulties for a given instrument
        /// </summary>
        /// <param name="instrument">Instrument</param>
        /// <param name="difficulty">DifficultyMask</param>
        /// <returns>bool</returns>
        public bool HasDifficultyForInstrument(Instrument instrument, DifficultyMask difficulty)
        {
            PartValues? part = instrument switch
            {
                Instrument.FiveFretGuitar     => _parts.FiveFretGuitar,
                Instrument.FiveFretBass       => _parts.FiveFretBass,
                Instrument.FiveFretRhythm     => _parts.FiveFretRhythm,
                Instrument.FiveFretCoopGuitar => _parts.FiveFretCoopGuitar,
                Instrument.Keys               => _parts.Keys,

                Instrument.SixFretGuitar     => _parts.SixFretGuitar,
                Instrument.SixFretBass       => _parts.SixFretBass,
                Instrument.SixFretRhythm     => _parts.SixFretRhythm,
                Instrument.SixFretCoopGuitar => _parts.SixFretCoopGuitar,

                Instrument.FourLaneDrums => _parts.FourLaneDrums,
                Instrument.FiveLaneDrums => _parts.FiveLaneDrums,
                Instrument.ProDrums      => _parts.ProDrums,

                Instrument.EliteDrums => _parts.EliteDrums,

                Instrument.ProGuitar_17Fret => _parts.ProGuitar_17Fret,
                Instrument.ProGuitar_22Fret => _parts.ProGuitar_22Fret,
                Instrument.ProBass_17Fret   => _parts.ProBass_17Fret,
                Instrument.ProBass_22Fret   => _parts.ProBass_22Fret,

                Instrument.ProKeys => _parts.ProKeys,

                Instrument.Vocals  => _parts.LeadVocals,
                Instrument.Harmony => _parts.HarmonyVocals,
                Instrument.Band    => _parts.BandDifficulty,
                _ => throw new ArgumentException("Unhandled instrument", nameof(instrument))
            };

            return (difficulty & part.Value.Difficulties) == difficulty;
        }

        public bool HasDifficultyForInstrument(Instrument instrument, Difficulty difficulty)
        {
            return HasDifficultyForInstrument(instrument, difficulty.ToDifficultyMask());
        }

        /// <summary>
        /// Checks that song has the full set of EMHX difficulties for a given instrument
        /// </summary>
        /// <param name="instrument">Instrument</param>
        /// <returns>bool</returns>
        public bool HasEmhxDifficultiesForInstrument(Instrument instrument)
        {
            var mask = Difficulty.Easy.ToDifficultyMask() | Difficulty.Medium.ToDifficultyMask() |
                Difficulty.Hard.ToDifficultyMask() | Difficulty.Expert.ToDifficultyMask();
            return HasDifficultyForInstrument(instrument, mask);
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

            stream.Write(_metadata.LinkBandcamp);
            stream.Write(_metadata.LinkBluesky);
            stream.Write(_metadata.LinkFacebook);
            stream.Write(_metadata.LinkInstagram);
            stream.Write(_metadata.LinkNewgrounds);
            stream.Write(_metadata.LinkSoundcloud);
            stream.Write(_metadata.LinkSpotify);
            stream.Write(_metadata.LinkTiktok);
            stream.Write(_metadata.LinkTwitter);
            stream.Write(_metadata.LinkOther);
            stream.Write(_metadata.LinkYoutube);

            stream.Write(_metadata.Location);

            stream.Write(_metadata.CreditAlbumArtDesignedBy);
            stream.Write(_metadata.CreditArrangedBy);
            stream.Write(_metadata.CreditComposedBy);
            stream.Write(_metadata.CreditCourtesyOf);
            stream.Write(_metadata.CreditEngineeredBy);
            stream.Write(_metadata.CreditLicense);
            stream.Write(_metadata.CreditMasteredBy);
            stream.Write(_metadata.CreditMixedBy);
            stream.Write(_metadata.CreditOther);
            stream.Write(_metadata.CreditPerformedBy);
            stream.Write(_metadata.CreditProducedBy);
            stream.Write(_metadata.CreditPublishedBy);
            stream.Write(_metadata.CreditWrittenBy);

            stream.Write(_metadata.CharterBass);
            stream.Write(_metadata.CharterDrums);
            stream.Write(_metadata.CharterEliteDrums);
            stream.Write(_metadata.CharterGuitar);
            stream.Write(_metadata.CharterKeys);
            stream.Write(_metadata.CharterLowerDiff);
            stream.Write(_metadata.CharterProBass);
            stream.Write(_metadata.CharterProKeys);
            stream.Write(_metadata.CharterProGuitar);
            stream.Write(_metadata.CharterVenue);
            stream.Write(_metadata.CharterVocals);

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

            _metadata.LinkBandcamp = stream.ReadString();
            _metadata.LinkBluesky = stream.ReadString();
            _metadata.LinkFacebook = stream.ReadString();
            _metadata.LinkInstagram = stream.ReadString();
            _metadata.LinkNewgrounds = stream.ReadString();
            _metadata.LinkSoundcloud = stream.ReadString();
            _metadata.LinkSpotify = stream.ReadString();
            _metadata.LinkTiktok = stream.ReadString();
            _metadata.LinkTwitter = stream.ReadString();
            _metadata.LinkOther = stream.ReadString();
            _metadata.LinkYoutube = stream.ReadString();

            _metadata.Location = stream.ReadString();

            _metadata.CreditAlbumArtDesignedBy = stream.ReadString();
            _metadata.CreditArrangedBy = stream.ReadString();
            _metadata.CreditComposedBy = stream.ReadString();
            _metadata.CreditCourtesyOf = stream.ReadString();
            _metadata.CreditEngineeredBy = stream.ReadString();
            _metadata.CreditLicense = stream.ReadString();
            _metadata.CreditMasteredBy = stream.ReadString();
            _metadata.CreditMixedBy = stream.ReadString();
            _metadata.CreditOther = stream.ReadString();
            _metadata.CreditPerformedBy = stream.ReadString();
            _metadata.CreditProducedBy = stream.ReadString();
            _metadata.CreditPublishedBy = stream.ReadString();
            _metadata.CreditWrittenBy = stream.ReadString();

            _metadata.CharterBass = stream.ReadString();
            _metadata.CharterDrums = stream.ReadString();
            _metadata.CharterEliteDrums = stream.ReadString();
            _metadata.CharterGuitar = stream.ReadString();
            _metadata.CharterKeys = stream.ReadString();
            _metadata.CharterLowerDiff = stream.ReadString();
            _metadata.CharterProBass = stream.ReadString();
            _metadata.CharterProKeys = stream.ReadString();
            _metadata.CharterProGuitar = stream.ReadString();
            _metadata.CharterVenue = stream.ReadString();
            _metadata.CharterVocals = stream.ReadString();

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