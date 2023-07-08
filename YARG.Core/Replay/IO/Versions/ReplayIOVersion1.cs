using System;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;

namespace YARG.Core.Replay.IO.Versions
{
    public class ReplayIoVersion1 : ReplayReadWriter
    {
        public override void WriteReplayData(BinaryWriter writer, Replay replay)
        {
            writer.Write(replay.SongName);
            writer.Write(replay.ArtistName);
            writer.Write(replay.CharterName);
            writer.Write(replay.BandScore);
            writer.Write(replay.Date.ToBinary());
            writer.Write(replay.SongChecksum);

            writer.Write(replay.PlayerCount);
            for (int i = 0; i < replay.PlayerCount; i++)
            {
                writer.Write(replay.PlayerNames[i]);
            }

            for (int i = 0; i < replay.PlayerCount; i++)
            {
                WriteFrame(writer, replay.Frames[i]);
            }
        }

        public override ReplayReadResult ReadReplayData(BinaryReader reader, Replay replay)
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
            writer.Write(frame.PlayerId);
            writer.Write(frame.PlayerName);
            writer.Write((int) frame.Instrument);
            writer.Write((int) frame.Difficulty);

            WriteInstrumentFrame(writer, frame);
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

        protected override void WriteBaseStats(BinaryWriter writer, BaseStats stats)
        {
            writer.Write(stats.Score);
            writer.Write(stats.Combo);
            writer.Write(stats.MaxCombo);

            writer.Write(stats.ScoreMultiplier);

            writer.Write(stats.NotesHit);
            writer.Write(stats.NotesMissed);

            writer.Write(stats.StarPowerAmount);

            writer.Write(stats.IsStarPowerActive);

            writer.Write(stats.PhrasesHit);
            writer.Write(stats.PhrasesMissed);
        }

        protected override void ReadBaseStats(BinaryReader reader, BaseStats stats)
        {
            stats.Score = reader.ReadInt32();
            stats.Combo = reader.ReadInt32();
            stats.MaxCombo = reader.ReadInt32();

            stats.ScoreMultiplier = reader.ReadInt32();

            stats.NotesHit = reader.ReadInt32();
            stats.NotesMissed = reader.ReadInt32();

            stats.StarPowerAmount = reader.ReadDouble();

            stats.IsStarPowerActive = reader.ReadBoolean();

            stats.PhrasesHit = reader.ReadInt32();
            stats.PhrasesMissed = reader.ReadInt32();
        }

        protected override void WriteGuitarStats(BinaryWriter writer, ReplayFrame<GuitarStats> frame)
        {
            WriteBaseStats(writer, frame.Stats);

            writer.Write(frame.Stats.Overstrums);
            writer.Write(frame.Stats.HoposStrummed);
            writer.Write(frame.Stats.GhostInputs);
        }

        protected override void ReadGuitarStats(BinaryReader reader, ReplayFrame<GuitarStats> frame)
        {
            frame.Stats = new GuitarStats();
            ReadBaseStats(reader, frame.Stats);

            frame.Stats.Overstrums = reader.ReadInt32();
            frame.Stats.HoposStrummed = reader.ReadInt32();
            frame.Stats.GhostInputs = reader.ReadInt32();
        }

        protected override void WriteDrumsStats(BinaryWriter writer, ReplayFrame<DrumStats> frame)
        {
            WriteBaseStats(writer, frame.Stats);
        }

        protected override void ReadDrumsStats(BinaryReader reader, ReplayFrame<DrumStats> frame)
        {
            frame.Stats = new DrumStats();
            ReadBaseStats(reader, frame.Stats);
        }
    }
}