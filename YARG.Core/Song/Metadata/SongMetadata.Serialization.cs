using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        public SongMetadata(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            _name.Str = strings.titles[reader.ReadInt32()];
            _artist.Str = strings.artists[reader.ReadInt32()];
            _album.Str = strings.albums[reader.ReadInt32()];
            _genre.Str = strings.genres[reader.ReadInt32()];
            Year = strings.years[reader.ReadInt32()];
            _charter.Str = strings.charters[reader.ReadInt32()];
            _playlist.Str = strings.playlists[reader.ReadInt32()];
            _source.Str = strings.sources[reader.ReadInt32()];

            _isMaster = reader.ReadBoolean();
            _previewStart = reader.ReadUInt64();
            _previewEnd = reader.ReadUInt64();
            _albumTrack = reader.ReadInt32();
            _playlistTrack = reader.ReadInt32();
            _songLength = reader.ReadUInt64();
            _chartOffset = reader.ReadInt64();
            _icon = reader.ReadLEBString();
            _loadingPhrase = reader.ReadLEBString();

            _parseSettings = new()
            {
                HopoThreshold = reader.ReadInt64(),
                HopoFreq_FoF = reader.ReadInt32(),
                EighthNoteHopo = reader.ReadBoolean(),
                SustainCutoffThreshold = reader.ReadInt64(),
                NoteSnapThreshold = reader.ReadInt64(),
                StarPowerNote = reader.ReadInt32()
            };

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
            writer.Write(_previewStart);
            writer.Write(_previewEnd);
            writer.Write(_albumTrack);
            writer.Write(_playlistTrack);
            writer.Write(_songLength);
            writer.Write(_chartOffset);
            writer.Write(_icon);
            writer.Write(_loadingPhrase);

            writer.Write(_parseSettings.HopoThreshold);
            writer.Write(_parseSettings.HopoFreq_FoF);
            writer.Write(_parseSettings.EighthNoteHopo);
            writer.Write(_parseSettings.SustainCutoffThreshold);
            writer.Write(_parseSettings.NoteSnapThreshold);
            writer.Write(_parseSettings.StarPowerNote);

            _parts.Serialize(writer);
            _hash.Serialize(writer);
        }
    }
}
