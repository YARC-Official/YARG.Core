using System;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Chart;

namespace YARG.Core.Song
{
    public partial struct SongMetadata
    {
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
