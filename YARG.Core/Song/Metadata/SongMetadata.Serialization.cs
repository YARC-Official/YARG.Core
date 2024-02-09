using System;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        public SongMetadata(BinaryReader reader, CategoryCacheStrings strings)
        {
            _name = strings.titles[reader.ReadInt32()];
            _artist = strings.artists[reader.ReadInt32()];
            _album = strings.albums[reader.ReadInt32()];
            _genre = strings.genres[reader.ReadInt32()];
            Year = strings.years[reader.ReadInt32()];
            _charter = strings.charters[reader.ReadInt32()];
            _playlist = strings.playlists[reader.ReadInt32()];
            _source = strings.sources[reader.ReadInt32()];

            _isMaster = reader.ReadBoolean();

            _albumTrack = reader.ReadInt32();
            _playlistTrack = reader.ReadInt32();

            _songLength = reader.ReadUInt64();
            _songOffset = reader.ReadInt64();

            _previewStart = reader.ReadUInt64();
            _previewEnd = reader.ReadUInt64();

            VideoStartTimeSeconds = reader.ReadDouble();
            VideoEndTimeSeconds = reader.ReadDouble();

            _loadingPhrase = reader.ReadString();

            _parseSettings.HopoThreshold = reader.ReadInt64();
            _parseSettings.HopoFreq_FoF = reader.ReadInt32();
            _parseSettings.EighthNoteHopo = reader.ReadBoolean();
            _parseSettings.SustainCutoffThreshold = reader.ReadInt64();
            _parseSettings.NoteSnapThreshold = reader.ReadInt64();
            _parseSettings.StarPowerNote = reader.ReadInt32();
            _parseSettings.DrumsType = (DrumsType)reader.ReadInt32();

            _parts = new(reader);
            _hash = HashWrapper.Deserialize(reader);
        }

        public void Serialize(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(node.title);
            writer.Write(node.artist);
            writer.Write(node.album);
            writer.Write(node.genre);
            writer.Write(node.year);
            writer.Write(node.charter);
            writer.Write(node.playlist);
            writer.Write(node.source);

            writer.Write(_isMaster);

            writer.Write(_albumTrack);
            writer.Write(_playlistTrack);

            writer.Write(_songLength);
            writer.Write(_songOffset);

            writer.Write(_previewStart);
            writer.Write(_previewEnd);

            writer.Write(VideoStartTimeSeconds);
            writer.Write(VideoEndTimeSeconds);

            writer.Write(_loadingPhrase);

            writer.Write(_parseSettings.HopoThreshold);
            writer.Write(_parseSettings.HopoFreq_FoF);
            writer.Write(_parseSettings.EighthNoteHopo);
            writer.Write(_parseSettings.SustainCutoffThreshold);
            writer.Write(_parseSettings.NoteSnapThreshold);
            writer.Write(_parseSettings.StarPowerNote);
            writer.Write((int)_parseSettings.DrumsType);

            _parts.Serialize(writer);
            _hash.Serialize(writer);
        }
    }
}
