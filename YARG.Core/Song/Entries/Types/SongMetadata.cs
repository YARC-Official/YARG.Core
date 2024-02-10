using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public struct SongMetadata
    {
        public static readonly SortString DEFAULT_NAME = "Unknown Name";
        public static readonly SortString DEFAULT_ARTIST = "Unknown Artist";
        public static readonly SortString DEFAULT_ALBUM = "Unknown Album";
        public static readonly SortString DEFAULT_GENRE = "Unknown Genre";
        public static readonly SortString DEFAULT_CHARTER = "Unknown Charter";
        public static readonly SortString DEFAULT_SOURCE = "Unknown Source";
        public const string DEFAULT_YEAR = "Unknown Year";

        public static readonly SongMetadata Default = new()
        {
            Name = SortString.Empty,
            Artist = DEFAULT_ARTIST,
            Album = DEFAULT_ALBUM,
            Genre = DEFAULT_GENRE,
            Charter = DEFAULT_CHARTER,
            Source = DEFAULT_SOURCE,
            Playlist = SortString.Empty,
            IsMaster = true,
            AlbumTrack = 0,
            PlaylistTrack = 0,
            Hash = default,
            Parts = new(),
            ParseSettings = ParseSettings.Default,
            LoadingPhrase = string.Empty,
            _unmodifiedYear = DEFAULT_YEAR,
            _parsedYear = DEFAULT_YEAR,
            _intYear = int.MaxValue,
            _songLength = 0,
            _songOffset = 0,
            _previewStart = 0,
            _previewEnd = 0,
            _videoStartTime = 0,
            _videoEndTime = -1,
        };

        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        public const int SIZEOF_DATETIME = 8;
        public const double MILLISECOND_FACTOR = 1000.0;

        public SortString Name;
        public SortString Artist;
        public SortString Album;
        public SortString Genre;
        public SortString Charter;
        public SortString Source;
        public SortString Playlist;

        public bool IsMaster;

        public int AlbumTrack;
        public int PlaylistTrack;

        public HashWrapper Hash;

        public AvailableParts Parts;

        public ParseSettings ParseSettings;

        public string LoadingPhrase;

        private string _unmodifiedYear;
        private string _parsedYear;
        private int _intYear;

        private ulong _songLength;
        private long _songOffset;

        private ulong _previewStart;
        private ulong _previewEnd;

        private long _videoStartTime;
        private long _videoEndTime;

        public string Year
        {
            readonly get => _parsedYear;
            set
            {
                _unmodifiedYear = value;
                var match = s_YearRegex.Match(value);
                if (string.IsNullOrEmpty(match.Value))
                    _parsedYear = value;
                else
                {
                    _parsedYear = match.Value[..4];
                    _intYear = int.Parse(_parsedYear);
                }
            }
        }

        public readonly string UnmodifiedYear => _unmodifiedYear;

        public int YearAsNumber
        {
            readonly get => _intYear;
            set
            {
                _intYear = value;
                _parsedYear = _unmodifiedYear = value.ToString();
            }
        }

        public ulong SongLengthMilliseconds
        {
            readonly get => _songLength;
            set => _songLength = value;
        }

        public long SongOffsetMilliseconds
        {
            readonly get => _songOffset;
            set => _songOffset = value;
        }

        public double SongLengthSeconds
        {
            readonly get => SongLengthMilliseconds / MILLISECOND_FACTOR;
            set => SongLengthMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double SongOffsetSeconds
        {
            readonly get => SongOffsetMilliseconds / MILLISECOND_FACTOR;
            set => SongOffsetMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public ulong PreviewStartMilliseconds
        {
            readonly get => _previewStart;
            set => _previewStart = value;
        }

        public ulong PreviewEndMilliseconds
        {
            readonly get => _previewEnd;
            set => _previewEnd = value;
        }

        public double PreviewStartSeconds
        {
            readonly get => PreviewStartMilliseconds / MILLISECOND_FACTOR;
            set => PreviewStartMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public double PreviewEndSeconds
        {
            readonly get => PreviewEndMilliseconds / MILLISECOND_FACTOR;
            set => PreviewEndMilliseconds = (ulong) (value * MILLISECOND_FACTOR);
        }

        public long VideoStartTimeMilliseconds
        {
            readonly get => _videoStartTime;
            set => _videoStartTime = value;
        }

        public long VideoEndTimeMilliseconds
        {
            readonly get => _videoEndTime;
            set => _videoEndTime = value;
        }

        public double VideoStartTimeSeconds
        {
            readonly get => VideoStartTimeMilliseconds / MILLISECOND_FACTOR;
            set => VideoStartTimeMilliseconds = (long) (value * MILLISECOND_FACTOR);
        }

        public double VideoEndTimeSeconds
        {
            readonly get => VideoEndTimeMilliseconds >= 0 ? VideoEndTimeMilliseconds / MILLISECOND_FACTOR : -1;
            set => VideoEndTimeMilliseconds = value >= 0 ? (long) (value * MILLISECOND_FACTOR) : -1;
        }

        public readonly override string ToString() { return Artist + " | " + Name; }

        public SongMetadata(AvailableParts parts, HashWrapper hash, IniSection section, string defaultPlaylist)
        {
            Parts = parts;
            Hash = hash;
            ParseSettings = ParseSettings.Default;
            ParseSettings.DrumsType = parts.GetDrumType();

            section.TryGet("name", out Name, DEFAULT_NAME);
            section.TryGet("artist", out Artist, DEFAULT_ARTIST);
            section.TryGet("album", out Album, DEFAULT_ALBUM);
            section.TryGet("genre", out Genre, DEFAULT_GENRE);

            if (!section.TryGet("charter", out Charter, DEFAULT_CHARTER))
            {
                section.TryGet("frets", out Charter, DEFAULT_CHARTER);
            }

            section.TryGet("icon", out Source, DEFAULT_SOURCE);
            section.TryGet("playlist", out Playlist, defaultPlaylist);

            _unmodifiedYear = DEFAULT_YEAR;
            _parsedYear = DEFAULT_YEAR;
            _intYear = int.MaxValue;

            _songLength = 0;
            _songOffset = 0;

            _previewStart = 0;
            _previewEnd = 0;

            _videoStartTime = 0;
            _videoEndTime = -1;

            section.TryGet("loading_phrase", out LoadingPhrase);

            if (!section.TryGet("playlist_track", out PlaylistTrack))
                PlaylistTrack = -1;

            if (!section.TryGet("album_track", out AlbumTrack))
                AlbumTrack = -1;

            section.TryGet("song_length", out _songLength);

            section.TryGet("video_start_time", out _videoStartTime);
            _videoEndTime = section.TryGet("video_end_time", out long videoEndTime) ? videoEndTime : -1000;

            if (!section.TryGet("hopo_frequency", out ParseSettings.HopoThreshold))
                ParseSettings.HopoThreshold = -1;

            if (!section.TryGet("hopofreq", out ParseSettings.HopoFreq_FoF))
                ParseSettings.HopoFreq_FoF = -1;

            section.TryGet("eighthnote_hopo", out ParseSettings.EighthNoteHopo);

            if (!section.TryGet("sustain_cutoff_threshold", out ParseSettings.SustainCutoffThreshold))
                ParseSettings.SustainCutoffThreshold = -1;

            if (!section.TryGet("multiplier_note", out ParseSettings.StarPowerNote))
                ParseSettings.StarPowerNote = -1;

            IsMaster = !section.TryGet("tags", out string tag) || tag.ToLower() != "cover";

            if (section.TryGet("year", out _unmodifiedYear))
            {
                Year = _unmodifiedYear;
            }
            else if (section.TryGet("year_chart", out _unmodifiedYear))
            {
                if (_unmodifiedYear.StartsWith(", "))
                    Year = _unmodifiedYear[2..];
                else if (_unmodifiedYear.StartsWith(','))
                    Year = _unmodifiedYear[1..];
                else
                    Year = _unmodifiedYear;
            }
            else
                _unmodifiedYear = DEFAULT_YEAR;

            if (!section.TryGet("preview", out _previewStart, out _previewEnd))
            {
                if (!section.TryGet("preview_start_time", out _previewStart) &&
                    section.TryGet("previewStart", out double previewStartSeconds))
                    PreviewStartSeconds = previewStartSeconds;

                if (!section.TryGet("preview_end_time", out _previewEnd) &&
                    section.TryGet("previewEnd", out double previewEndSeconds))
                    PreviewEndSeconds = previewEndSeconds;
            }


            if (!section.TryGet("delay", out _songOffset) || _songOffset == 0)
            {
                if (section.TryGet("offset", out double songOffsetSeconds))
                {
                    SongOffsetSeconds = songOffsetSeconds;
                }
            }
        }

        public SongMetadata(BinaryReader reader, CategoryCacheStrings strings)
        {
            _unmodifiedYear = DEFAULT_YEAR;
            _parsedYear = DEFAULT_YEAR;
            _intYear = int.MaxValue;

            Name = strings.titles[reader.ReadInt32()];
            Artist = strings.artists[reader.ReadInt32()];
            Album = strings.albums[reader.ReadInt32()];
            Genre = strings.genres[reader.ReadInt32()];

            _unmodifiedYear = strings.years[reader.ReadInt32()];
            var match = s_YearRegex.Match(_unmodifiedYear);
            if (string.IsNullOrEmpty(match.Value))
                _parsedYear = _unmodifiedYear;
            else
            {
                _parsedYear = match.Value[..4];
                _intYear = int.Parse(_parsedYear);
            }

            Charter = strings.charters[reader.ReadInt32()];
            Playlist = strings.playlists[reader.ReadInt32()];
            Source = strings.sources[reader.ReadInt32()];

            IsMaster = reader.ReadBoolean();

            AlbumTrack = reader.ReadInt32();
            PlaylistTrack = reader.ReadInt32();

            _songLength = reader.ReadUInt64();
            _songOffset = reader.ReadInt64();

            _previewStart = reader.ReadUInt64();
            _previewEnd = reader.ReadUInt64();

            _videoStartTime = reader.ReadInt64();
            _videoEndTime = reader.ReadInt64();

            LoadingPhrase = reader.ReadString();

            ParseSettings = new ParseSettings()
            {
                HopoThreshold = reader.ReadInt64(),
                HopoFreq_FoF = reader.ReadInt32(),
                EighthNoteHopo = reader.ReadBoolean(),
                SustainCutoffThreshold = reader.ReadInt64(),
                NoteSnapThreshold = reader.ReadInt64(),
                StarPowerNote = reader.ReadInt32(),
                DrumsType = (DrumsType) reader.ReadInt32(),
            };

            Parts = new(reader);
            Hash = HashWrapper.Deserialize(reader);
        }

        public readonly void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(node.title);
            writer.Write(node.artist);
            writer.Write(node.album);
            writer.Write(node.genre);
            writer.Write(node.year);
            writer.Write(node.charter);
            writer.Write(node.playlist);
            writer.Write(node.source);

            writer.Write(IsMaster);

            writer.Write(AlbumTrack);
            writer.Write(PlaylistTrack);

            writer.Write(_songLength);
            writer.Write(_songOffset);

            writer.Write(_previewStart);
            writer.Write(_previewEnd);

            writer.Write(_videoStartTime);
            writer.Write(_videoEndTime);

            writer.Write(LoadingPhrase);

            writer.Write(ParseSettings.HopoThreshold);
            writer.Write(ParseSettings.HopoFreq_FoF);
            writer.Write(ParseSettings.EighthNoteHopo);
            writer.Write(ParseSettings.SustainCutoffThreshold);
            writer.Write(ParseSettings.NoteSnapThreshold);
            writer.Write(ParseSettings.StarPowerNote);
            writer.Write((int) ParseSettings.DrumsType);

            Parts.Serialize(writer);
            Hash.Serialize(writer);
        }
    }
}
