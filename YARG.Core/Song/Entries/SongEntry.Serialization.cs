using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public abstract partial class SongEntry
    {
        protected static SongMetadata DeserializeMetadata(BinaryReader reader, CategoryCacheStrings strings)
        {
            SongMetadata metadata = default;
            metadata.Name = strings.titles[reader.ReadInt32()];
            metadata.Artist = strings.artists[reader.ReadInt32()];
            metadata.Album = strings.albums[reader.ReadInt32()];
            metadata.Genre = strings.genres[reader.ReadInt32()];

            metadata.Year = strings.years[reader.ReadInt32()];
            metadata.Charter = strings.charters[reader.ReadInt32()];
            metadata.Playlist = strings.playlists[reader.ReadInt32()];
            metadata.Source = strings.sources[reader.ReadInt32()];

            metadata.IsMaster = reader.ReadBoolean();

            metadata.AlbumTrack = reader.ReadInt32();
            metadata.PlaylistTrack = reader.ReadInt32();

            metadata.SongLength = reader.ReadUInt64();
            metadata.SongOffset = reader.ReadInt64();

            metadata.PreviewStart = reader.ReadUInt64();
            metadata.PreviewEnd = reader.ReadUInt64();

            metadata.VideoStartTime = reader.ReadInt64();
            metadata.VideoEndTime = reader.ReadInt64();

            metadata.LoadingPhrase = reader.ReadString();

            metadata.ParseSettings = new ParseSettings()
            {
                HopoThreshold = reader.ReadInt64(),
                HopoFreq_FoF = reader.ReadInt32(),
                EighthNoteHopo = reader.ReadBoolean(),
                SustainCutoffThreshold = reader.ReadInt64(),
                NoteSnapThreshold = reader.ReadInt64(),
                StarPowerNote = reader.ReadInt32(),
                DrumsType = (DrumsType) reader.ReadInt32(),
            };

            metadata.Parts = new(reader);
            metadata.Hash = HashWrapper.Deserialize(reader);
            return metadata;
        }

        protected void SerializeMetadata(BinaryWriter writer, CategoryCacheWriteNode node)
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
