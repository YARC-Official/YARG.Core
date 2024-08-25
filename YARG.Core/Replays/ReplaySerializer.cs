using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song;
using YARG.Core.Song.Cache;

namespace YARG.Core.Replays
{
    public static partial class ReplaySerializer
    {
        #region Replay

        public static void SerializeReplay(BinaryWriter writer, Replay replay)
        {
            var blockStream = new MemoryStream();
            var blockWriter = new BinaryWriter(blockStream);

            writer.BaseStream.Seek(ReplayHeader.SIZE, SeekOrigin.Begin);

            // Metadata
            Sections.SerializeMetadata(blockWriter, replay.Metadata);
            WriteBlock(writer, blockStream);

            // PresetContainer
            Sections.SerializePresetContainer(blockWriter, replay.PresetContainer);
            WriteBlock(writer, blockStream);

            writer.Write(replay.PlayerCount);
            foreach (var playerName in replay.PlayerNames)
            {
                writer.Write(playerName);
            }

            foreach (var frame in replay.Frames)
            {
                Sections.SerializeFrame(blockWriter, frame);
                WriteBlock(writer, blockStream);
            }

            writer.BaseStream.Seek(ReplayHeader.SIZE, SeekOrigin.Begin);

            HashWrapper.Algorithm.ComputeHash(writer.BaseStream);

            var hashWrapper = HashWrapper.Create( HashWrapper.Algorithm.ComputeHash(writer.BaseStream));
            replay.Header.ReplayChecksum = hashWrapper;

            writer.BaseStream.Seek(0, SeekOrigin.Begin);

            Sections.SerializeHeader(writer, replay.Header);
        }

        public static (ReplayReadResult Result, Replay? Replay) DeserializeReplay(UnmanagedMemoryStream stream, int version = 0)
        {
            var replay = new Replay();

            long position = 0;

            UnmanagedMemoryStream sliceStream;

            // Metadata
            replay.Metadata = Sections.DeserializeMetadata(stream, version);

            // PresetContainer
            replay.PresetContainer = Sections.DeserializePresetContainer(stream, version);

            // Player names
            int playerCount = stream.Read<int>(Endianness.Little);

            // Hard limit on player count to prevent OOM
            if (playerCount > 255)
            {
                return (ReplayReadResult.Corrupted, null);
            }

            var playerNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerNames[i] = stream.ReadString();
            }

            // Player Frames
            replay.Frames = new ReplayFrame[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                replay.Frames[i] = Sections.DeserializeFrame(stream, version);
            }

            return (ReplayReadResult.Valid, replay);
        }

        #endregion

        private static int WriteBlock(BinaryWriter writer, MemoryStream blockStream)
        {
            var blockLength = (int) blockStream.Position;
            writer.Write(blockLength);

            var block = new ReadOnlySpan<byte>(blockStream.GetBuffer(), 0, blockLength);
            writer.BaseStream.Write(block);
            blockStream.Position = 0;

            return blockLength;
        }

        private static UnmanagedMemoryStream ReadBlock(UnmanagedMemoryStream stream)
        {
            var blockLength = ReadBlockLength(stream);
            return stream.Slice(blockLength);
        }

        private static int ReadBlockLength(UnmanagedMemoryStream stream)
        {
            return stream.Read<int>(Endianness.Little);
        }
    }
}