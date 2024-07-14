using System;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
        DuplicateFilesFound,
        IniEntryCorruption,
        IniNotDownloaded,
        ChartNotDownloaded,
        NoName,
        NoNotes,
        DTAError,
        MoggError,
        UnsupportedEncryption,
        MissingMidi,
        MissingUpdateMidi,
        MissingUpgradeMidi,
        PossibleCorruption,
        FailedSngLoad,

        NoAudio,
        PathTooLong,
        MultipleMidiTrackNames,
        MultipleMidiTrackNames_Update,
        MultipleMidiTrackNames_Upgrade,

        LooseChart_Warning,
    }

    /// <summary>
    /// The type of chart file to read.
    /// </summary>
    public enum ChartType
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
        public const double MILLISECOND_FACTOR = 1000.0;
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

        private string _parsedYear;
        private int _intYear;

        protected SongMetadata _metadata;
        protected AvailableParts _parts;
        protected ParseSettings _parseSettings;
        protected HashWrapper _hash;

        public abstract string Directory { get; }

        public abstract EntryType SubType { get; }

        public SortString Name => _metadata.Name;
        public SortString Artist => _metadata.Artist;
        public SortString Album => _metadata.Album;
        public SortString Genre => _metadata.Genre;
        public SortString Charter => _metadata.Charter;
        public SortString Source => _metadata.Source;
        public SortString Playlist => _metadata.Playlist;

        public string Year => _parsedYear;

        public string UnmodifiedYear => _metadata.Year;

        public int YearAsNumber
        {
            get => _intYear;
            set
            {
                _intYear = value;
                _parsedYear = _metadata.Year = value.ToString();
            }
        }

        public bool IsMaster => _metadata.IsMaster;

        public int AlbumTrack => _metadata.AlbumTrack;

        public int PlaylistTrack => _metadata.PlaylistTrack;

        public string LoadingPhrase => _metadata.LoadingPhrase;

        public ulong SongLengthMilliseconds
        {
            get => _metadata.SongLength;
            set => _metadata.SongLength = value;
        }

        public long SongOffsetMilliseconds
        {
            get => _metadata.SongOffset;
            set => _metadata.SongOffset = value;
        }

        public double SongLengthSeconds
        {
            get => _metadata.SongLength / MILLISECOND_FACTOR;
            set => _metadata.SongLength = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            get => _metadata.SongOffset / MILLISECOND_FACTOR;
            set => _metadata.SongOffset = (long) (value * MILLISECOND_FACTOR);
        }

        public long PreviewStartMilliseconds
        {
            get => _metadata.PreviewStart;
            set => _metadata.PreviewStart = value;
        }

        public long PreviewEndMilliseconds
        {
            get => _metadata.PreviewEnd;
            set => _metadata.PreviewEnd = value;
        }

        public double PreviewStartSeconds
        {
            get => _metadata.PreviewStart / MILLISECOND_FACTOR;
            set => _metadata.PreviewStart = (long) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            get => _metadata.PreviewEnd / MILLISECOND_FACTOR;
            set => _metadata.PreviewEnd = (long) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            get => _metadata.VideoStartTime;
            set => _metadata.VideoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            get => _metadata.VideoEndTime;
            set => _metadata.VideoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            get => _metadata.VideoStartTime / MILLISECOND_FACTOR;
            set => _metadata.VideoStartTime = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            get => _metadata.VideoEndTime >= 0 ? _metadata.VideoEndTime / MILLISECOND_FACTOR : -1;
            set => _metadata.VideoEndTime = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public HashWrapper Hash => _hash;

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

        public override string ToString() { return _metadata.Artist + " | " + _metadata.Name; }

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

                    // Instrument.TrueDrums => _parts.TrueDrums,

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
                Instrument.FiveFretGuitar => _parts.FiveFretGuitar.SubTracks > 0,
                Instrument.FiveFretBass => _parts.FiveFretBass.SubTracks > 0,
                Instrument.FiveFretRhythm => _parts.FiveFretRhythm.SubTracks > 0,
                Instrument.FiveFretCoopGuitar => _parts.FiveFretCoopGuitar.SubTracks > 0,
                Instrument.Keys => _parts.Keys.SubTracks > 0,

                Instrument.SixFretGuitar => _parts.SixFretGuitar.SubTracks > 0,
                Instrument.SixFretBass => _parts.SixFretBass.SubTracks > 0,
                Instrument.SixFretRhythm => _parts.SixFretRhythm.SubTracks > 0,
                Instrument.SixFretCoopGuitar => _parts.SixFretCoopGuitar.SubTracks > 0,

                Instrument.FourLaneDrums => _parts.FourLaneDrums.SubTracks > 0,
                Instrument.FiveLaneDrums => _parts.FiveLaneDrums.SubTracks > 0,
                Instrument.ProDrums => _parts.ProDrums.SubTracks > 0,

                // Instrument.TrueDrums => _parts.TrueDrums.SubTracks > 0,

                Instrument.ProGuitar_17Fret => _parts.ProGuitar_17Fret.SubTracks > 0,
                Instrument.ProGuitar_22Fret => _parts.ProGuitar_22Fret.SubTracks > 0,
                Instrument.ProBass_17Fret => _parts.ProBass_17Fret.SubTracks > 0,
                Instrument.ProBass_22Fret => _parts.ProBass_22Fret.SubTracks > 0,

                Instrument.ProKeys => _parts.ProKeys.SubTracks > 0,

                // Instrument.Dj => _parts.DJ.SubTracks > 0,

                Instrument.Vocals => _parts.LeadVocals.SubTracks > 0,
                Instrument.Harmony => _parts.HarmonyVocals.SubTracks > 0,
                Instrument.Band => _parts.BandDifficulty.SubTracks > 0,

                _ => false
            };
        }

        protected SongEntry()
        {
            _metadata = SongMetadata.Default;
            _parts = AvailableParts.Default;
            _parseSettings = ParseSettings.Default;
            _parsedYear = SongMetadata.DEFAULT_YEAR;
            _intYear = int.MaxValue;
        }

        private static readonly SortString DEFAULT_NAME_SORT = SortString.Convert(SongMetadata.DEFAULT_NAME);
        private static readonly SortString DEFAULT_ARTIST_SORT = SortString.Convert(SongMetadata.DEFAULT_ARTIST);
        private static readonly SortString DEFAULT_ALBUM_SORT = SortString.Convert(SongMetadata.DEFAULT_ALBUM);
        private static readonly SortString DEFAULT_GENRE_SORT = SortString.Convert(SongMetadata.DEFAULT_GENRE);
        private static readonly SortString DEFAULT_CHARTER_SORT = SortString.Convert(SongMetadata.DEFAULT_CHARTER);
        private static readonly SortString DEFAULT_SOURCE_SORT = SortString.Convert(SongMetadata.DEFAULT_SOURCE);

        protected SongEntry(in AvailableParts parts, in HashWrapper hash, IniSection modifiers, in string defaultPlaylist)
        {
            _parts = parts;
            _hash = hash;

            modifiers.TryGet("name", out _metadata.Name, DEFAULT_NAME_SORT);
            modifiers.TryGet("artist", out _metadata.Artist, DEFAULT_ARTIST_SORT);
            modifiers.TryGet("album", out _metadata.Album, DEFAULT_ALBUM_SORT);
            modifiers.TryGet("genre", out _metadata.Genre, DEFAULT_GENRE_SORT);

            if (!modifiers.TryGet("year", out _metadata.Year))
            {
                // Capitalization = from .chart
                if (modifiers.TryGet("Year", out _metadata.Year))
                {
                    if (_metadata.Year.StartsWith(", "))
                    {
                        _metadata.Year = _metadata.Year[2..];
                    }
                    else if (_metadata.Year.StartsWith(','))
                    {
                        _metadata.Year = _metadata.Year[1..];
                    }
                }
                else
                {
                    _metadata.Year = SongMetadata.DEFAULT_YEAR;
                }
            }

            _intYear = ParseYear(in _metadata.Year, out _parsedYear)
                ? int.Parse(_parsedYear)
                : int.MaxValue;

            if (!modifiers.TryGet("charter", out _metadata.Charter, DEFAULT_CHARTER_SORT))
            {
                modifiers.TryGet("frets", out _metadata.Charter, DEFAULT_CHARTER_SORT);
            }

            modifiers.TryGet("icon", out _metadata.Source, DEFAULT_SOURCE_SORT);
            modifiers.TryGet("playlist", out _metadata.Playlist, defaultPlaylist);

            modifiers.TryGet("loading_phrase", out _metadata.LoadingPhrase);

            if (!modifiers.TryGet("playlist_track", out _metadata.PlaylistTrack))
            {
                _metadata.PlaylistTrack = -1;
            }

            if (!modifiers.TryGet("album_track", out _metadata.AlbumTrack))
            {
                _metadata.AlbumTrack = -1;
            }

            modifiers.TryGet("song_length", out _metadata.SongLength);
            modifiers.TryGet("rating", out _metadata.SongRating);

            modifiers.TryGet("video_start_time", out _metadata.VideoStartTime);
            if (!modifiers.TryGet("video_end_time", out _metadata.VideoEndTime))
            {
                _metadata.VideoEndTime = -1;
            }

            if (!modifiers.TryGet("preview", out _metadata.PreviewStart, out _metadata.PreviewEnd))
            {
                if (!modifiers.TryGet("preview_start_time", out _metadata.PreviewStart))
                {
                    // Capitlization = from .chart
                    if (modifiers.TryGet("PreviewStart", out double previewStartSeconds))
                    {
                        _metadata.PreviewStart = (long) (previewStartSeconds * MILLISECOND_FACTOR);
                    }
                    else
                    {
                        _metadata.PreviewStart = -1;
                    }
                }

                if (!modifiers.TryGet("preview_end_time", out _metadata.PreviewEnd))
                {
                    // Capitlization = from .chart
                    if (modifiers.TryGet("PreviewEnd", out double previewEndSeconds))
                    {
                        _metadata.PreviewEnd = (long) (previewEndSeconds * MILLISECOND_FACTOR);
                    }
                    else
                    {
                        _metadata.PreviewEnd = -1;
                    }
                }
            }

            if (!modifiers.TryGet("delay", out _metadata.SongOffset) || _metadata.SongOffset == 0)
            {
                // Capitlization = from .chart
                if (modifiers.TryGet("Offset", out double songOffsetSeconds))
                {
                    _metadata.SongOffset = (long) (songOffsetSeconds * MILLISECOND_FACTOR);
                }
            }

            if (parts.FourLaneDrums.SubTracks > 0)
            {
                _parseSettings.DrumsType = DrumsType.FourLane;
            }
            else if (parts.FiveLaneDrums.SubTracks > 0)
            {
                _parseSettings.DrumsType = DrumsType.FiveLane;
            }
            else
            {
                _parseSettings.DrumsType = DrumsType.Unknown;
            }

            if (!modifiers.TryGet("hopo_frequency", out _parseSettings.HopoThreshold))
            {
                _parseSettings.HopoThreshold = -1;
            }

            if (!modifiers.TryGet("hopofreq", out _parseSettings.HopoFreq_FoF))
            {
                _parseSettings.HopoFreq_FoF = -1;
            }

            modifiers.TryGet("eighthnote_hopo", out _parseSettings.EighthNoteHopo);

            if (!modifiers.TryGet("sustain_cutoff_threshold", out _parseSettings.SustainCutoffThreshold))
            {
                _parseSettings.SustainCutoffThreshold = -1;
            }

            if (!modifiers.TryGet("multiplier_note", out _parseSettings.StarPowerNote))
            {
                _parseSettings.StarPowerNote = -1;
            }

            _metadata.IsMaster = !modifiers.TryGet("tags", out string tag) || tag.ToLower() != "cover";
        }

        protected SongEntry(UnmanagedMemoryStream stream, in CategoryCacheStrings strings)
        {
            _metadata.Name = strings.titles[stream.Read<int>(Endianness.Little)];
            _metadata.Artist = strings.artists[stream.Read<int>(Endianness.Little)];
            _metadata.Album = strings.albums[stream.Read<int>(Endianness.Little)];
            _metadata.Genre = strings.genres[stream.Read<int>(Endianness.Little)];

            _metadata.Year = strings.years[stream.Read<int>(Endianness.Little)];
            _metadata.Charter = strings.charters[stream.Read<int>(Endianness.Little)];
            _metadata.Playlist = strings.playlists[stream.Read<int>(Endianness.Little)];
            _metadata.Source = strings.sources[stream.Read<int>(Endianness.Little)];

            _metadata.IsMaster = stream.ReadBoolean();

            _metadata.AlbumTrack = stream.Read<int>(Endianness.Little);
            _metadata.PlaylistTrack = stream.Read<int>(Endianness.Little);

            _metadata.SongLength = stream.Read<ulong>(Endianness.Little);
            _metadata.SongOffset = stream.Read<long>(Endianness.Little);
            _metadata.SongRating = stream.Read<uint>(Endianness.Little);

            _metadata.PreviewStart = stream.Read<long>(Endianness.Little);
            _metadata.PreviewEnd = stream.Read<long>(Endianness.Little);

            _metadata.VideoStartTime = stream.Read<long>(Endianness.Little);
            _metadata.VideoEndTime = stream.Read<long>(Endianness.Little);

            _metadata.LoadingPhrase = stream.ReadString();

            _parseSettings.HopoThreshold = stream.Read<long>(Endianness.Little);
            _parseSettings.HopoFreq_FoF = stream.Read<int>(Endianness.Little);
            _parseSettings.EighthNoteHopo = stream.ReadBoolean();
            _parseSettings.SustainCutoffThreshold = stream.Read<long>(Endianness.Little);
            _parseSettings.NoteSnapThreshold = stream.Read<long>(Endianness.Little);
            _parseSettings.StarPowerNote = stream.Read<int>(Endianness.Little);
            _parseSettings.DrumsType = (DrumsType) stream.Read<int>(Endianness.Little);

            unsafe
            {
                fixed (AvailableParts* ptr = &_parts)
                {
                    stream.Read(new Span<byte>(ptr, sizeof(AvailableParts)));
                }
            }
            _hash = HashWrapper.Deserialize(stream);

            _intYear = ParseYear(in _metadata.Year, out _parsedYear)
               ? int.Parse(_parsedYear)
               : int.MaxValue;
        }

        protected void SerializeMetadata(in BinaryWriter writer, in CategoryCacheWriteNode node)
        {
            writer.Write(node.title);
            writer.Write(node.artist);
            writer.Write(node.album);
            writer.Write(node.genre);
            writer.Write(node.year);
            writer.Write(node.charter);
            writer.Write(node.playlist);
            writer.Write(node.source);

            writer.Write(_metadata.IsMaster);

            writer.Write(_metadata.AlbumTrack);
            writer.Write(_metadata.PlaylistTrack);

            writer.Write(_metadata.SongLength);
            writer.Write(_metadata.SongOffset);
            writer.Write(_metadata.SongRating);

            writer.Write(_metadata.PreviewStart);
            writer.Write(_metadata.PreviewEnd);

            writer.Write(_metadata.VideoStartTime);
            writer.Write(_metadata.VideoEndTime);

            writer.Write(_metadata.LoadingPhrase);

            writer.Write(_parseSettings.HopoThreshold);
            writer.Write(_parseSettings.HopoFreq_FoF);
            writer.Write(_parseSettings.EighthNoteHopo);
            writer.Write(_parseSettings.SustainCutoffThreshold);
            writer.Write(_parseSettings.NoteSnapThreshold);
            writer.Write(_parseSettings.StarPowerNote);
            writer.Write((int) _parseSettings.DrumsType);

            unsafe
            {
                fixed (AvailableParts* ptr = &_parts)
                {
                    writer.Write(new Span<byte>(ptr, sizeof(AvailableParts)));
                }
            }
            _hash.Serialize(writer);
        }

        private static bool ParseYear(in string baseString, out string parsedString)
        {
            for (int i = 0; i <= baseString.Length - 4; ++i)
            {
                int pivot = i;
                while (i < pivot + 4 && i < baseString.Length && char.IsDigit(baseString[i]))
                {
                    ++i;
                }

                if (i == pivot + 4)
                {
                    parsedString = baseString[pivot..i];
                    return true;
                }
            }
            parsedString = baseString;
            return false;
        }
    }
}