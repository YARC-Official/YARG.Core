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
            _name = strings.titles[reader.Read<int>(Endianness.Little)];
            _artist = strings.artists[reader.Read<int>(Endianness.Little)];
            _album = strings.albums[reader.Read<int>(Endianness.Little)];
            _genre = strings.genres[reader.Read<int>(Endianness.Little)];
            Year = strings.years[reader.Read<int>(Endianness.Little)];
            _charter = strings.charters[reader.Read<int>(Endianness.Little)];
            _playlist = strings.playlists[reader.Read<int>(Endianness.Little)];
            _source = strings.sources[reader.Read<int>(Endianness.Little)];

            _isMaster = reader.ReadBoolean();

            _albumTrack = reader.Read<int>(Endianness.Little);
            _playlistTrack = reader.Read<int>(Endianness.Little);

            _songLength = reader.Read<ulong>(Endianness.Little);
            _songOffset = reader.Read<long>(Endianness.Little);

            _previewStart = reader.Read<ulong>(Endianness.Little);
            _previewEnd = reader.Read<ulong>(Endianness.Little);

            VideoStartTimeSeconds = reader.Read<double>(Endianness.Little);
            VideoEndTimeSeconds = reader.Read<double>(Endianness.Little);

            _loadingPhrase = reader.ReadLEBString();

            _parseSettings.HopoThreshold = reader.Read<long>(Endianness.Little);
            _parseSettings.HopoFreq_FoF = reader.Read<int>(Endianness.Little);
            _parseSettings.EighthNoteHopo = reader.ReadBoolean();
            _parseSettings.SustainCutoffThreshold = reader.Read<long>(Endianness.Little);
            _parseSettings.NoteSnapThreshold = reader.Read<long>(Endianness.Little);
            _parseSettings.StarPowerNote = reader.Read<int>(Endianness.Little);
            _parseSettings.DrumsType = (DrumsType)reader.Read<int>(Endianness.Little);

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
