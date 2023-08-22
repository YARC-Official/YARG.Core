using System;
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

            _albumTrack = reader.ReadInt32();
            _playlistTrack = reader.ReadInt32();

            _songLength = reader.ReadDouble();
            _songOffset = reader.ReadDouble();

            _previewStart = reader.ReadDouble();
            _previewEnd = reader.ReadDouble();

            _videoStartTime = reader.ReadDouble();
            _videoEndTime = reader.ReadDouble();

            _loadingPhrase = reader.ReadLEBString();

            _parseSettings.HopoThreshold = reader.ReadInt64();
            _parseSettings.HopoFreq_FoF = reader.ReadInt32();
            _parseSettings.EighthNoteHopo = reader.ReadBoolean();
            _parseSettings.SustainCutoffThreshold = reader.ReadInt64();
            _parseSettings.NoteSnapThreshold = reader.ReadInt64();
            _parseSettings.StarPowerNote = reader.ReadInt32();

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

            writer.Write(_videoStartTime);
            writer.Write(_videoEndTime);

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

#nullable enable
        private static AbridgedFileInfo? ParseFileInfo(YARGBinaryReader reader)
        {
            return ParseFileInfo(reader.ReadLEBString(), reader);
        }

        private static AbridgedFileInfo? ParseFileInfo(string file, YARGBinaryReader reader)
        {
            FileInfo midiInfo = new(file);
            if (!midiInfo.Exists || midiInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return midiInfo;
        }
    }
}
