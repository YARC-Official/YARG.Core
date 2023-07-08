using System;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;

namespace YARG.Core.Replay.IO.Versions
{
    public class ReplayIoVersion1 : ReplayReadWriter
    {
        public ReplayIoVersion1(int version) : base(version)
        {
        }

        protected override void WriteContent(BinaryWriter writer, Replay replay)
        {
            throw new System.NotImplementedException();
        }

        protected override ReplayReadResult ReadContent(BinaryReader reader, Replay replay)
        {
            replay.SongName = reader.ReadString();
            replay.ArtistName = reader.ReadString();
            replay.CharterName = reader.ReadString();
            replay.BandScore = reader.ReadInt32();
            replay.Date = DateTime.FromBinary(reader.ReadInt64());
            replay.SongChecksum = reader.ReadString();

            replay.PlayerCount = reader.ReadInt32();
            replay.PlayerNames = new string[replay.PlayerCount];
            for (int i = 0; i < replay.PlayerCount; i++)
            {
                replay.PlayerNames[i] = reader.ReadString();
            }

            replay.Frames = new ReplayFrame[replay.PlayerCount];
            for (int i = 0; i < replay.PlayerCount; i++)
            {
                ReadFrame(reader, out var frame);
                replay.Frames[i] = frame;
            }

            return ReplayReadResult.Valid;
        }

        protected override void WriteFrame(BinaryWriter writer, ReplayFrame frame)
        {
            throw new NotImplementedException();
        }

        protected override ReplayReadResult ReadFrame(BinaryReader reader, out ReplayFrame frame)
        {
            int playerId = reader.ReadInt32();
            string playerName = reader.ReadString();
            var instrument = (Instrument) reader.ReadInt32();
            var difficulty = (Difficulty) reader.ReadInt32();

            switch (instrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    frame = new ReplayFrame<GuitarStats>();
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    frame = new ReplayFrame<DrumStats>();
                    break;
                case GameMode.ProGuitar:
                case GameMode.Vocals:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            frame.PlayerId = playerId;
            frame.PlayerName = playerName;
            frame.Instrument = instrument;
            frame.Difficulty = difficulty;

            ReadInstrumentFrame(reader, frame);

            return ReplayReadResult.Valid;
        }

        protected override void ReadFrameStats(BinaryReader reader, BaseStats stats)
        {
            throw new NotImplementedException();
        }

        protected override void ReadGuitarFrame(BinaryReader reader, ReplayFrame<GuitarStats> frame)
        {
            throw new NotImplementedException();
        }

        protected override void ReadDrumsFrame(BinaryReader reader, ReplayFrame<DrumStats> frame)
        {
            throw new NotImplementedException();
        }
    }
}