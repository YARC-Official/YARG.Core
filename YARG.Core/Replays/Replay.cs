﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using YARG.Core.Game;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayHeader : IBinarySerializable
    {
        public EightCC     Magic;
        public int         ReplayVersion;
        public int         EngineVersion;
        public HashWrapper ReplayChecksum;

        public void Serialize(BinaryWriter writer)
        {
            Magic.Serialize(writer);
            writer.Write(ReplayVersion);
            writer.Write(EngineVersion);

            ReplayChecksum.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            Magic = EightCC.Read(reader);
            ReplayVersion = reader.ReadInt32();
            EngineVersion = reader.ReadInt32();
            ReplayChecksum = new HashWrapper(reader);
        }
    }

    public class Replay : IBinarySerializable
    {
        public string      SongName;
        public string      ArtistName;
        public string      CharterName;
        public int         BandScore;
        public double      ReplayLength;
        public DateTime    Date;
        public HashWrapper SongChecksum;

        public int            ColorProfileCount;
        public ColorProfile[] ColorProfiles;

        public int      PlayerCount;
        public string[] PlayerNames;

        public ReplayFrame[] Frames;

        public Replay()
        {
            SongName = string.Empty;
            ArtistName = string.Empty;
            CharterName = string.Empty;

            ColorProfiles = Array.Empty<ColorProfile>();
            PlayerNames = Array.Empty<string>();
            Frames = Array.Empty<ReplayFrame>();
        }

        public Replay(BinaryReader reader, int version = 0)
        {
            Deserialize(reader, version);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SongName);
            writer.Write(ArtistName);
            writer.Write(CharterName);
            writer.Write(BandScore);
            writer.Write(ReplayLength);
            writer.Write(Date.ToBinary());

            SongChecksum.Serialize(writer);

            writer.Write(ColorProfileCount);
            for (int i = 0; i < ColorProfileCount; i++)
            {
                ColorProfiles[i].Serialize(writer);
            }

            writer.Write(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
            {
                writer.Write(PlayerNames[i]);
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                Frames[i].Serialize(writer);
            }
        }

        [MemberNotNull(nameof(SongName))]
        [MemberNotNull(nameof(ArtistName))]
        [MemberNotNull(nameof(CharterName))]
        [MemberNotNull(nameof(ColorProfiles))]
        [MemberNotNull(nameof(PlayerNames))]
        [MemberNotNull(nameof(Frames))]
        public void Deserialize(BinaryReader reader, int version = 0)
        {
            SongName = reader.ReadString();
            ArtistName = reader.ReadString();
            CharterName = reader.ReadString();
            BandScore = reader.ReadInt32();
            ReplayLength = reader.ReadDouble();
            Date = DateTime.FromBinary(reader.ReadInt64());
            SongChecksum = new HashWrapper(reader);

            ColorProfileCount = reader.ReadInt32();
            ColorProfiles = new ColorProfile[ColorProfileCount];

            for (int i = 0; i < ColorProfileCount; i++)
            {
                ColorProfiles[i] = new ColorProfile("");
                ColorProfiles[i].Deserialize(reader, version);
            }

            PlayerCount = reader.ReadInt32();
            PlayerNames = new string[PlayerCount];

            Frames = new ReplayFrame[PlayerCount];
            for (int i = 0; i < PlayerCount; i++)
            {
                PlayerNames[i] = reader.ReadString();
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                Frames[i] = new ReplayFrame();
                Frames[i].Deserialize(reader, version);
            }
        }
    }
}