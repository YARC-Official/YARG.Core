using System;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Song;
using YARG.Core.Utility;

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

        public static (ReplayReadResult Result, Replay? Replay) DeserializeReplay(byte[] replayFileData, int version = 0)
        {
            var replay = new Replay();

            var position = 0;
            var wholeFileSpan = replayFileData.AsSpan();

            var headerSpan = wholeFileSpan[position..0x20];
            var spanReader = new SpanBinaryReader(headerSpan);

            var header = Sections.DeserializeHeader(ref spanReader, version);
            if (header == null)
            {
                return (ReplayReadResult.NotAReplay, null);
            }

            position += spanReader.Position;

            var hash = HashWrapper.Algorithm.ComputeHash(replayFileData, position, replayFileData.Length - position);
            var computedChecksum = HashWrapper.Create(hash);

            if(!header.Value.ReplayChecksum.Equals(computedChecksum))
            {
                return (ReplayReadResult.Corrupted, null);
            }

            // Metadata
            //position = spanReader.Position;
            spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, 2));
            var metadataLength = ReadBlockLength(ref spanReader);

            position += spanReader.Position;
            spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, metadataLength));
            replay.Metadata = Sections.DeserializeMetadata(ref spanReader, version);

            // PresetContainer
            position += spanReader.Position;
            spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, 2));
            var presetContainerLength = ReadBlockLength(ref spanReader);

            position += spanReader.Position;
            spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, presetContainerLength));
            replay.PresetContainer = Sections.DeserializePresetContainer(ref spanReader, version);

            // Player names
            position += spanReader.Position;
            spanReader = new SpanBinaryReader(wholeFileSpan[position..]);

            int playerCount = spanReader.ReadInt32();

            // Hard limit on player count to prevent OOM
            if (playerCount > 255)
            {
                return (ReplayReadResult.Corrupted, null);
            }

            var playerNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerNames[i] = spanReader.ReadString();
            }

            // Player Frames
            position += spanReader.Position;

            replay.Frames = new ReplayFrame[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, 2));
                var frameLength = ReadBlockLength(ref spanReader);
                position += spanReader.Position;

                spanReader = new SpanBinaryReader(wholeFileSpan.Slice(position, frameLength));

                replay.Frames[i] = Sections.DeserializeFrame(ref spanReader, version);
            }

            return (ReplayReadResult.Valid, replay);
        }

        #endregion

        private static int WriteBlock(BinaryWriter writer, Stream blockStream)
        {
            var length = WriteBlockLength(writer, blockStream);
            blockStream.SetLength(length);
            blockStream.CopyTo(writer.BaseStream);
            blockStream.Position = 0;

            return length;
        }

        private static int WriteBlockLength(BinaryWriter writer, Stream blockStream)
        {
            var length = (int) blockStream.Position;

            writer.Write(length);
            blockStream.Position = 0;

            return length;
        }

        private static int ReadBlockLength(ref SpanBinaryReader reader)
        {
            return reader.ReadInt32();
        }
    }
}