using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Replay.IO.Versions;

namespace YARG.Core.Replay.IO
{
    public enum ReplayReadResult
    {
        Valid,
        NotAReplay,
        InvalidVersion,
        Corrupted,
    }

    public static class ReplayIO
    {
        private const long REPLAY_MAGIC_HEADER = 0x59414C5047524159;
        private const int  REPLAY_VERSION      = 1;

        private static readonly Dictionary<int, ReplayReadWriter> ReadWriters = new()
        {
            { 1, new ReplayIoVersion1() },
        };

        public static void WriteReplay(string path, Replay replay)
        {
            using var writer = new BinaryWriter(File.OpenWrite(path));
            WriteData(writer, replay);
        }

        public static byte[] WriteReplay(Replay replay)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteData(writer, replay);

            return stream.ToArray();
        }

        public static ReplayReadResult ReadReplay(string path, out Replay replay)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            return ReadData(reader, out replay);
        }

        public static ReplayReadResult ReadReplay(byte[] data, out Replay replay)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            return ReadData(reader, out replay);
        }

        private static void WriteData(BinaryWriter writer, Replay replay)
        {
            using var contentStream = new MemoryStream();
            using var contentWriter = new BinaryWriter(contentStream);

            if (!ReadWriters.TryGetValue(REPLAY_VERSION, out var readWriter))
            {
                throw new InvalidOperationException($"No read writer for replay version {REPLAY_VERSION}");
            }

            readWriter.WriteReplayData(contentWriter, replay);

            byte[] checksum = SHA1.Create().ComputeHash(contentStream);
            string checksumString = BitConverter.ToString(checksum).Replace("-", string.Empty);

            replay.Header.ReplayChecksum = checksumString;

            WriteHeader(writer, replay);
            contentStream.Seek(0, SeekOrigin.Begin);
            contentStream.CopyTo(writer.BaseStream);
        }

        private static ReplayReadResult ReadData(BinaryReader reader, out Replay replay)
        {
            replay = new Replay();

            var headerResult = ReadHeader(reader, replay);
            if (headerResult != ReplayReadResult.Valid)
            {
                return headerResult;
            }

            if (!ReadWriters.TryGetValue(replay.Header.ReplayVersion, out var readWriter))
            {
                return ReplayReadResult.InvalidVersion;
            }

            return readWriter.ReadReplayData(reader, replay);
        }

        private static void WriteHeader(BinaryWriter writer, Replay replay)
        {
            writer.Write(REPLAY_MAGIC_HEADER);
            writer.Write(REPLAY_VERSION);

            // Skip over checksum for now
            writer.Seek(20, SeekOrigin.Current);

            writer.Write(replay.Header.GameVersion);
        }

        private static ReplayReadResult ReadHeader(BinaryReader reader, Replay replay)
        {
            var header = new ReplayHeader
            {
                Magic = reader.ReadInt64(),
                ReplayVersion = reader.ReadInt32(),
                ReplayChecksum = reader.ReadString(),
                GameVersion = reader.ReadInt32(),
            };

            if (header.Magic != REPLAY_MAGIC_HEADER)
            {
                return ReplayReadResult.NotAReplay;
            }

            long position = reader.BaseStream.Position;

            // Compute checksum of replay data and compare it to the one in the header
            byte[] replayData = reader.ReadBytes((int) (reader.BaseStream.Length - reader.BaseStream.Position));

            reader.BaseStream.Seek(position, SeekOrigin.Begin);

            byte[] checksum = SHA1.Create().ComputeHash(replayData);
            string checksumString = BitConverter.ToString(checksum).Replace("-", string.Empty);

            if (header.ReplayChecksum != checksumString)
            {
                return ReplayReadResult.Corrupted;
            }

            return ReplayReadResult.Valid;
        }
    }
}