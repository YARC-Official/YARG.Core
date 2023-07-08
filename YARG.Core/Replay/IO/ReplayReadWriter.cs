using System;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;

namespace YARG.Core.Replay.IO
{
    public abstract class ReplayReadWriter
    {
        public const long REPLAY_MAGIC_HEADER = 0x59414C5047524159;

        private readonly int _version;

        protected ReplayReadWriter(int version)
        {
            _version = version;
        }

        public void Write(Replay replay)
        {
        }

        public ReplayReadResult Read(string path, out Replay replay)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            return Read(reader, out replay);
        }

        public ReplayReadResult Read(byte[] data, out Replay replay)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            return Read(reader, out replay);
        }

        private ReplayReadResult Read(BinaryReader reader, out Replay replay)
        {
            replay = new Replay();

            var headerResult = ReadHeader(reader, replay);
            if (headerResult != ReplayReadResult.Valid)
            {
                return headerResult;
            }

            return ReadContent(reader, replay);
        }

        private void WriteHeader(BinaryWriter writer, Replay replay)
        {
        }

        private ReplayReadResult ReadHeader(BinaryReader reader, Replay replay)
        {
            var header = new ReplayHeader
            {
                Magic = reader.ReadInt64(),
            };

            if (header.Magic != REPLAY_MAGIC_HEADER)
            {
                return ReplayReadResult.NotAReplay;
            }

            header.ReplayVersion = reader.ReadInt32();
            if (header.ReplayVersion != _version)
            {
                return ReplayReadResult.InvalidVersion;
            }

            long preHashPosition = reader.BaseStream.Position;

            byte[] allData = reader.ReadBytes((int) (reader.BaseStream.Length - reader.BaseStream.Position));
            byte[] computedChecksum = SHA1.Create().ComputeHash(allData);
            string computedChecksumString = BitConverter.ToString(computedChecksum).Replace("-", string.Empty);

            reader.BaseStream.Seek(preHashPosition, SeekOrigin.Begin);

            header.ReplayChecksum = reader.ReadString();
            if (header.ReplayChecksum != computedChecksumString)
            {
                return ReplayReadResult.Corrupted;
            }

            header.GameVersion = reader.ReadInt32();

            return ReplayReadResult.Valid;
        }

        protected void ReadInstrumentFrame(BinaryReader reader, ReplayFrame frame)
        {
            switch (frame.Instrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    ReadGuitarFrame(reader, frame as ReplayFrame<GuitarStats>);
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    ReadDrumsFrame(reader, frame as ReplayFrame<DrumStats>);
                    break;
                case GameMode.ProGuitar:
                case GameMode.Vocals:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected abstract void WriteContent(BinaryWriter writer, Replay replay);
        protected abstract ReplayReadResult ReadContent(BinaryReader reader, Replay replay);

        protected abstract void WriteFrame(BinaryWriter writer, ReplayFrame frame);

        protected abstract ReplayReadResult
            ReadFrame(BinaryReader reader, out ReplayFrame frame);

        protected abstract void ReadFrameStats(BinaryReader reader, BaseStats stats);

        protected abstract void ReadGuitarFrame(BinaryReader reader, ReplayFrame<GuitarStats> frame);
        protected abstract void ReadDrumsFrame(BinaryReader reader, ReplayFrame<DrumStats> frame);
    }
}