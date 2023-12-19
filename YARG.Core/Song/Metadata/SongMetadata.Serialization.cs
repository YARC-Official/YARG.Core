using System;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Chart;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        public SongMetadata(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            _name.Str = strings.titles[reader.Read<int>()];
            _artist.Str = strings.artists[reader.Read<int>()];
            _album.Str = strings.albums[reader.Read<int>()];
            _genre.Str = strings.genres[reader.Read<int>()];
            Year = strings.years[reader.Read<int>()];
            _charter.Str = strings.charters[reader.Read<int>()];
            _playlist.Str = strings.playlists[reader.Read<int>()];
            _source.Str = strings.sources[reader.Read<int>()];

            _isMaster = reader.ReadBoolean();

            _albumTrack = reader.Read<int>();
            _playlistTrack = reader.Read<int>();

            _songLength = reader.Read<ulong>();
            _songOffset = reader.Read<long>();

            _previewStart = reader.Read<ulong>();
            _previewEnd = reader.Read<ulong>();

            VideoStartTimeSeconds = reader.Read<double>();
            VideoEndTimeSeconds = reader.Read<double>();

            _loadingPhrase = reader.ReadLEBString();

            _parseSettings.HopoThreshold = reader.Read<long>();
            _parseSettings.HopoFreq_FoF = reader.Read<int>();
            _parseSettings.EighthNoteHopo = reader.ReadBoolean();
            _parseSettings.SustainCutoffThreshold = reader.Read<long>();
            _parseSettings.NoteSnapThreshold = reader.Read<long>();
            _parseSettings.StarPowerNote = reader.Read<int>();
            _parseSettings.DrumsType = (DrumsType)reader.Read<int>();

            _parts = new(reader);
            _hash = new(reader);
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
