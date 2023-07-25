using System;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;

namespace YARG.Core.Replays.IO
{
    public abstract class ReplayReadWriter
    {
        protected void WriteInstrumentFrame(BinaryWriter writer, ReplayFrame frame)
        {
            var gameMode = frame.Instrument.ToGameMode();
            switch (gameMode)
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    WriteGuitarStats(writer, frame as ReplayFrame<GuitarStats>);
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    WriteDrumsStats(writer, frame as ReplayFrame<DrumStats>);
                    break;
                case GameMode.ProGuitar:
                case GameMode.Vocals:
                    break;
                default:
                    throw new NotImplementedException($"Unhandled game mode {gameMode}!");
            }
        }

        protected void ReadInstrumentFrame(BinaryReader reader, ReplayFrame frame)
        {
            var gameMode = frame.Instrument.ToGameMode();
            switch (gameMode)
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    ReadGuitarStats(reader, frame as ReplayFrame<GuitarStats>);
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    ReadDrumsStats(reader, frame as ReplayFrame<DrumStats>);
                    break;
                case GameMode.ProGuitar:
                case GameMode.Vocals:
                    break;
                default:
                    throw new NotImplementedException($"Unhandled game mode {gameMode}!");
            }
        }

        public abstract void WriteReplayData(BinaryWriter writer, Replay replay);
        public abstract ReplayReadResult ReadReplayData(BinaryReader reader, Replay replay);

        protected abstract void WriteFrame(BinaryWriter writer, ReplayFrame frame);
        protected abstract ReplayReadResult ReadFrame(BinaryReader reader, out ReplayFrame frame);

        protected abstract void WriteBaseStats(BinaryWriter writer, BaseStats stats);
        protected abstract void ReadBaseStats(BinaryReader reader, BaseStats frame);

        protected abstract void WriteGuitarStats(BinaryWriter writer, ReplayFrame<GuitarStats> frame);
        protected abstract void ReadGuitarStats(BinaryReader reader, ReplayFrame<GuitarStats> frame);

        protected abstract void WriteDrumsStats(BinaryWriter writer, ReplayFrame<DrumStats> frame);
        protected abstract void ReadDrumsStats(BinaryReader reader, ReplayFrame<DrumStats> frame);
    }
}