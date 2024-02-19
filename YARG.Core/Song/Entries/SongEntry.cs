using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Chart;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
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

        LooseChart_Warning
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
            ".png", ".jpg", ".jpeg"
        };

        protected static readonly string YARGROUND_EXTENSION = ".yarground";
        protected static readonly string YARGROUND_FULLNAME = "bg.yarground";

        private static readonly Random YARGROUND_RNG = new();
        protected static BackgroundResult? SelectRandomYarground(string directory)
        {
            var venues = new List<string>();
            foreach (var file in System.IO.Directory.EnumerateFiles(directory))
            {
                if (Path.GetExtension(file) == YARGROUND_EXTENSION)
                {
                    venues.Add(file);
                }
            }

            if (venues.Count == 0)
            {
                return null;
            }
            
            var stream = File.OpenRead(venues[YARGROUND_RNG.Next(venues.Count)]);
            return new BackgroundResult(BackgroundType.Yarground, stream);
        }

        private string _parsedYear;
        private int _intYear;

        protected SongMetadata Metadata;

        public abstract string Directory { get; }

        public abstract EntryType SubType { get; }

        public SortString Name => Metadata.Name;
        public SortString Artist => Metadata.Artist;
        public SortString Album => Metadata.Album;
        public SortString Genre => Metadata.Genre;
        public SortString Charter => Metadata.Charter;
        public SortString Source => Metadata.Source;
        public SortString Playlist => Metadata.Playlist;

        public string Year => _parsedYear;

        public string UnmodifiedYear => Metadata.Year;

        public int YearAsNumber
        {
            get => _intYear;
            set
            {
                _intYear = value;
                _parsedYear = Metadata.Year = value.ToString();
            }
        }

        public bool IsMaster => Metadata.IsMaster;

        public int AlbumTrack => Metadata.AlbumTrack;

        public int PlaylistTrack => Metadata.PlaylistTrack;

        public string LoadingPhrase => Metadata.LoadingPhrase;

        public ulong SongLengthMilliseconds
        {
            get => Metadata.SongLength;
            set => Metadata.SongLength = value;
        }

        public long SongOffsetMilliseconds
        {
            get => Metadata.SongOffset;
            set => Metadata.SongOffset = value;
        }

        public double SongLengthSeconds
        {
            get => Metadata.SongLength / MILLISECOND_FACTOR;
            set => Metadata.SongLength = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            get => Metadata.SongOffset / MILLISECOND_FACTOR;
            set => Metadata.SongOffset = (long) (value * MILLISECOND_FACTOR);
        }

        public ulong PreviewStartMilliseconds
        {
            get => Metadata.PreviewStart;
            set => Metadata.PreviewStart = value;
        }

        public ulong PreviewEndMilliseconds
        {
            get => Metadata.PreviewEnd;
            set => Metadata.PreviewEnd = value;
        }

        public double PreviewStartSeconds
        {
            get => Metadata.PreviewStart / MILLISECOND_FACTOR;
            set => Metadata.PreviewStart = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            get => Metadata.PreviewEnd / MILLISECOND_FACTOR;
            set => Metadata.PreviewEnd = (ulong) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            get => Metadata.VideoStartTime;
            set => Metadata.VideoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            get => Metadata.VideoEndTime;
            set => Metadata.VideoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            get => Metadata.VideoStartTime / MILLISECOND_FACTOR;
            set => Metadata.VideoStartTime = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            get => Metadata.VideoEndTime >= 0 ? Metadata.VideoEndTime / MILLISECOND_FACTOR : -1;
            set => Metadata.VideoEndTime = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public HashWrapper Hash => Metadata.Hash;

        public AvailableParts Parts => Metadata.Parts;

        public ParseSettings ParseSettings => Metadata.ParseSettings;

        public override string ToString() { return Metadata.Artist + " | " + Metadata.Name; }

        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        protected SongEntry()
        {
            Metadata = SongMetadata.Default;
            _parsedYear = SongMetadata.DEFAULT_YEAR;
            _intYear = int.MaxValue;
        }

        protected SongEntry(in AvailableParts parts, in HashWrapper hash, IniSection modifiers, in string defaultPlaylist)
        {
            Metadata.Parts = parts;
            Metadata.Hash = hash;
            Metadata.ParseSettings = ParseSettings.Default;
            Metadata.ParseSettings.DrumsType = parts.GetDrumType();

            modifiers.TryGet("name", out Metadata.Name, SongMetadata.DEFAULT_NAME);
            modifiers.TryGet("artist", out Metadata.Artist, SongMetadata.DEFAULT_ARTIST);
            modifiers.TryGet("album", out Metadata.Album, SongMetadata.DEFAULT_ALBUM);
            modifiers.TryGet("genre", out Metadata.Genre, SongMetadata.DEFAULT_GENRE);

            if (!modifiers.TryGet("year", out Metadata.Year))
            {
                if (modifiers.TryGet("year_chart", out Metadata.Year))
                {
                    if (Metadata.Year.StartsWith(", "))
                    {
                        Metadata.Year = Metadata.Year[2..];
                    }
                    else if (Metadata.Year.StartsWith(','))
                    {
                        Metadata.Year = Metadata.Year[1..];
                    }
                }
                else
                {
                    Metadata.Year = SongMetadata.DEFAULT_YEAR;
                }
            }

            var match = s_YearRegex.Match(Metadata.Year);
            if (string.IsNullOrEmpty(match.Value))
            {
                _parsedYear = Metadata.Year;
                _intYear = int.MaxValue;
            }
            else
            {
                _parsedYear = match.Value[..4];
                _intYear = int.Parse(_parsedYear);
            }

            if (!modifiers.TryGet("charter", out Metadata.Charter, SongMetadata.DEFAULT_CHARTER))
            {
                modifiers.TryGet("frets", out Metadata.Charter, SongMetadata.DEFAULT_CHARTER);
            }

            modifiers.TryGet("icon", out Metadata.Source, SongMetadata.DEFAULT_SOURCE);
            modifiers.TryGet("playlist", out Metadata.Playlist, defaultPlaylist);

            modifiers.TryGet("loading_phrase", out Metadata.LoadingPhrase);

            if (!modifiers.TryGet("playlist_track", out Metadata.PlaylistTrack))
            {
                Metadata.PlaylistTrack = -1;
            }

            if (!modifiers.TryGet("album_track", out Metadata.AlbumTrack))
            {
                Metadata.AlbumTrack = -1;
            }

            modifiers.TryGet("song_length", out Metadata.SongLength);
            modifiers.TryGet("rating", out Metadata.SongRating);

            modifiers.TryGet("video_start_time", out Metadata.VideoStartTime);
            if (!modifiers.TryGet("video_end_time", out Metadata.VideoEndTime))
            {
                Metadata.VideoEndTime = -1;
            }

            if (!modifiers.TryGet("preview", out Metadata.PreviewStart, out Metadata.PreviewEnd))
            {
                if (!modifiers.TryGet("preview_start_time", out Metadata.PreviewStart) && modifiers.TryGet("previewStart", out double previewStartSeconds))
                {
                    Metadata.PreviewStart = (ulong) (previewStartSeconds * MILLISECOND_FACTOR);
                }

                if (!modifiers.TryGet("preview_end_time", out Metadata.PreviewEnd) && modifiers.TryGet("previewEnd", out double previewEndSeconds))
                {
                    Metadata.PreviewEnd = (ulong) (previewEndSeconds * MILLISECOND_FACTOR);
                }
            }

            if (!modifiers.TryGet("delay", out Metadata.SongOffset) || Metadata.SongOffset == 0)
            {
                if (modifiers.TryGet("offset", out double songOffsetSeconds))
                {
                    Metadata.SongOffset = (long) (songOffsetSeconds * MILLISECOND_FACTOR);
                }
            }

            if (!modifiers.TryGet("hopo_frequency", out Metadata.ParseSettings.HopoThreshold))
            {
                Metadata.ParseSettings.HopoThreshold = -1;
            }

            if (!modifiers.TryGet("hopofreq", out Metadata.ParseSettings.HopoFreq_FoF))
            {
                Metadata.ParseSettings.HopoFreq_FoF = -1;
            }

            modifiers.TryGet("eighthnote_hopo", out Metadata.ParseSettings.EighthNoteHopo);

            if (!modifiers.TryGet("sustain_cutoff_threshold", out Metadata.ParseSettings.SustainCutoffThreshold))
            {
                Metadata.ParseSettings.SustainCutoffThreshold = -1;
            }

            if (!modifiers.TryGet("multiplier_note", out Metadata.ParseSettings.StarPowerNote))
            {
                Metadata.ParseSettings.StarPowerNote = -1;
            }

            Metadata.IsMaster = !modifiers.TryGet("tags", out string tag) || tag.ToLower() != "cover";
        }

        protected SongEntry(in BinaryReader reader, in CategoryCacheStrings strings)
        {
            Metadata.Name = strings.titles[reader.ReadInt32()];
            Metadata.Artist = strings.artists[reader.ReadInt32()];
            Metadata.Album = strings.albums[reader.ReadInt32()];
            Metadata.Genre = strings.genres[reader.ReadInt32()];

            Metadata.Year = strings.years[reader.ReadInt32()];
            Metadata.Charter = strings.charters[reader.ReadInt32()];
            Metadata.Playlist = strings.playlists[reader.ReadInt32()];
            Metadata.Source = strings.sources[reader.ReadInt32()];

            Metadata.IsMaster = reader.ReadBoolean();

            Metadata.AlbumTrack = reader.ReadInt32();
            Metadata.PlaylistTrack = reader.ReadInt32();

            Metadata.SongLength = reader.ReadUInt64();
            Metadata.SongOffset = reader.ReadInt64();
            Metadata.SongRating = reader.ReadUInt32();

            Metadata.PreviewStart = reader.ReadUInt64();
            Metadata.PreviewEnd = reader.ReadUInt64();

            Metadata.VideoStartTime = reader.ReadInt64();
            Metadata.VideoEndTime = reader.ReadInt64();

            Metadata.LoadingPhrase = reader.ReadString();
            Metadata.ParseSettings = new ParseSettings()
            {
                HopoThreshold = reader.ReadInt64(),
                HopoFreq_FoF = reader.ReadInt32(),
                EighthNoteHopo = reader.ReadBoolean(),
                SustainCutoffThreshold = reader.ReadInt64(),
                NoteSnapThreshold = reader.ReadInt64(),
                StarPowerNote = reader.ReadInt32(),
                DrumsType = (DrumsType) reader.ReadInt32(),
            };

            Metadata.Parts = new(reader);
            Metadata.Hash = HashWrapper.Deserialize(reader);

            var match = s_YearRegex.Match(Metadata.Year);
            if (string.IsNullOrEmpty(match.Value))
            {
                _parsedYear = Metadata.Year;
                _intYear = int.MaxValue;
            }
            else
            {
                _parsedYear = match.Value[..4];
                _intYear = int.Parse(_parsedYear);
            }
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

            writer.Write(Metadata.IsMaster);

            writer.Write(Metadata.AlbumTrack);
            writer.Write(Metadata.PlaylistTrack);

            writer.Write(Metadata.SongLength);
            writer.Write(Metadata.SongOffset);
            writer.Write(Metadata.SongRating);

            writer.Write(Metadata.PreviewStart);
            writer.Write(Metadata.PreviewEnd);

            writer.Write(Metadata.VideoStartTime);
            writer.Write(Metadata.VideoEndTime);

            writer.Write(Metadata.LoadingPhrase);

            writer.Write(Metadata.ParseSettings.HopoThreshold);
            writer.Write(Metadata.ParseSettings.HopoFreq_FoF);
            writer.Write(Metadata.ParseSettings.EighthNoteHopo);
            writer.Write(Metadata.ParseSettings.SustainCutoffThreshold);
            writer.Write(Metadata.ParseSettings.NoteSnapThreshold);
            writer.Write(Metadata.ParseSettings.StarPowerNote);
            writer.Write((int) Metadata.ParseSettings.DrumsType);

            Metadata.Parts.Serialize(writer);
            Metadata.Hash.Serialize(writer);
        }
    }
}