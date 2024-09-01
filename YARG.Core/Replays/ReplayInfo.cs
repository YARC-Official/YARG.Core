using System;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayInfo
    {
        public readonly string FilePath;
        public readonly string ReplayName;

        public readonly int ReplayVersion;
        public readonly int EngineVersion;
        public readonly HashWrapper ReplayChecksum;

        public readonly string SongName;
        public readonly string ArtistName;
        public readonly string CharterName;
        public readonly int BandScore;
        public readonly StarAmount BandStars;
        public readonly double ReplayLength;
        public readonly DateTime Date;
        public readonly HashWrapper SongChecksum;

        public ReplayInfo(string path, string replayName, int replayVersion, int engineVerion, in HashWrapper replayChecksum, string song, string artist, string charter, in HashWrapper songChecksum, in DateTime date, double length, int score, StarAmount stars)
        {
            FilePath = path;
            ReplayName = replayName;

            ReplayVersion = replayVersion;
            EngineVersion = engineVerion;
            ReplayChecksum = replayChecksum;

            SongName = song;
            ArtistName = artist;
            CharterName = charter;
            SongChecksum = songChecksum;
            Date = date;
            ReplayLength = length;
            BandScore = score;
            BandStars = stars;
        }

        public ReplayInfo(string path, UnmanagedMemoryStream stream)
        {
            FilePath = path;

            ReplayVersion = stream.Read<int>(Endianness.Little);
            EngineVersion = stream.Read<int>(Endianness.Little);
            ReplayChecksum = HashWrapper.Deserialize(stream);

            SongName = stream.ReadString();
            ArtistName = stream.ReadString();
            CharterName = stream.ReadString();
            SongChecksum = HashWrapper.Deserialize(stream);
            Date = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            ReplayLength = stream.Read<int>(Endianness.Little);
            BandScore = stream.Read<int>(Endianness.Little);
            BandStars = (StarAmount) stream.ReadByte();

            ReplayName = ConstructReplayName(SongName, ArtistName, CharterName, in Date);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ReplayVersion);
            writer.Write(EngineVersion);
            ReplayChecksum.Serialize(writer);

            writer.Write(SongName);
            writer.Write(ArtistName);
            writer.Write(CharterName);
            SongChecksum.Serialize(writer);
            writer.Write(Date.ToBinary());
            writer.Write(ReplayLength);
            writer.Write(BandScore);
            writer.Write((byte) BandStars);
        }

        // Remove invalid characters from the replay name
        private static readonly Regex ReplayNameRegex = new("[<>:\"/\\|?*]", RegexOptions.Compiled);
        public static string ConstructReplayName(string song, string artist, string charter, in DateTime date)
        {
            var strippedSong = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(song), "");
            var strippedArtist = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(artist), "");
            var strippedCharter = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(charter), "");

            return $"{strippedArtist}-{strippedSong}-{strippedCharter}-{date:yy-MM-dd-HH-mm-ss}";
        }
    }
}
