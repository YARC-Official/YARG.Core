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
        public const double MILLISECOND_FACTOR = 1000.0;

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
            Year = DEFAULT_YEAR,
            SongLength = 0,
            SongOffset = 0,
            PreviewStart = 0,
            PreviewEnd = 0,
            VideoStartTime = 0,
            VideoEndTime = -1,
        };

        public SortString Name;
        public SortString Artist;
        public SortString Album;
        public SortString Genre;
        public SortString Charter;
        public SortString Source;
        public SortString Playlist;

        public string Year;

        public ulong SongLength;
        public long SongOffset;

        public ulong PreviewStart;
        public ulong PreviewEnd;

        public long VideoStartTime;
        public long VideoEndTime;

        public bool IsMaster;

        public int AlbumTrack;
        public int PlaylistTrack;

        public HashWrapper Hash;

        public AvailableParts Parts;

        public ParseSettings ParseSettings;

        public string LoadingPhrase;

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

            if (!section.TryGet("year", out Year))
            {
                if (section.TryGet("year_chart", out Year))
                {
                    if (Year.StartsWith(", "))
                    {
                        Year = Year[2..];
                    }
                    else if (Year.StartsWith(','))
                    {
                        Year = Year[1..];
                    }
                }
                else
                {
                    Year = DEFAULT_YEAR;
                }
            }

            if (!section.TryGet("charter", out Charter, DEFAULT_CHARTER))
            {
                section.TryGet("frets", out Charter, DEFAULT_CHARTER);
            }

            section.TryGet("icon", out Source, DEFAULT_SOURCE);
            section.TryGet("playlist", out Playlist, defaultPlaylist);

            section.TryGet("loading_phrase", out LoadingPhrase);

            if (!section.TryGet("playlist_track", out PlaylistTrack))
            {
                PlaylistTrack = -1;
            }

            if (!section.TryGet("album_track", out AlbumTrack))
            {
                AlbumTrack = -1;
            }

            section.TryGet("song_length", out SongLength);

            section.TryGet("video_start_time", out VideoStartTime);
            if (!section.TryGet("video_end_time", out VideoEndTime))
            {
                VideoEndTime = -1;
            }

            if (!section.TryGet("preview", out PreviewStart, out PreviewEnd))
            {
                if (!section.TryGet("preview_start_time", out PreviewStart) && section.TryGet("previewStart", out double previewStartSeconds))
                {
                    PreviewStart = (ulong) (previewStartSeconds * MILLISECOND_FACTOR);
                }

                if (!section.TryGet("preview_end_time", out PreviewEnd) && section.TryGet("previewEnd", out double previewEndSeconds))
                {
                    PreviewEnd = (ulong) (previewEndSeconds * MILLISECOND_FACTOR);
                }
            }


            if (!section.TryGet("delay", out SongOffset) || SongOffset == 0)
            {
                if (section.TryGet("offset", out double songOffsetSeconds))
                {
                    SongOffset = (long) (songOffsetSeconds * MILLISECOND_FACTOR);
                }
            }

            if (!section.TryGet("hopo_frequency", out ParseSettings.HopoThreshold))
            {
                ParseSettings.HopoThreshold = -1;
            }

            if (!section.TryGet("hopofreq", out ParseSettings.HopoFreq_FoF))
            {
                ParseSettings.HopoFreq_FoF = -1;
            }

            section.TryGet("eighthnote_hopo", out ParseSettings.EighthNoteHopo);

            if (!section.TryGet("sustain_cutoff_threshold", out ParseSettings.SustainCutoffThreshold))
            {
                ParseSettings.SustainCutoffThreshold = -1;
            }

            if (!section.TryGet("multiplier_note", out ParseSettings.StarPowerNote))
            {
                ParseSettings.StarPowerNote = -1;
            }

            IsMaster = !section.TryGet("tags", out string tag) || tag.ToLower() != "cover";
        }

        public SongMetadata(BinaryReader reader, CategoryCacheStrings strings)
        {
            Name = strings.titles[reader.ReadInt32()];
            Artist = strings.artists[reader.ReadInt32()];
            Album = strings.albums[reader.ReadInt32()];
            Genre = strings.genres[reader.ReadInt32()];
            Year = strings.years[reader.ReadInt32()];
            Charter = strings.charters[reader.ReadInt32()];
            Playlist = strings.playlists[reader.ReadInt32()];
            Source = strings.sources[reader.ReadInt32()];

            IsMaster = reader.ReadBoolean();

            AlbumTrack = reader.ReadInt32();
            PlaylistTrack = reader.ReadInt32();

            SongLength = reader.ReadUInt64();
            SongOffset = reader.ReadInt64();

            PreviewStart = reader.ReadUInt64();
            PreviewEnd = reader.ReadUInt64();

            VideoStartTime = reader.ReadInt64();
            VideoEndTime = reader.ReadInt64();

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

            writer.Write(SongLength);
            writer.Write(SongOffset);

            writer.Write(PreviewStart);
            writer.Write(PreviewEnd);

            writer.Write(VideoStartTime);
            writer.Write(VideoEndTime);

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
