using System;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public static partial class ReplaySerializer
    {
        #region Replay

        public static void SerializeReplay(BinaryWriter writer, ReplayNew replayNew)
        {
            var blockStream = new MemoryStream();
            var blockWriter = new BinaryWriter(blockStream);

            // Header is always serialized into the main stream.
            Sections.SerializeHeader(writer, replayNew.Header);

            // Metadata
            Sections.SerializeMetadata(blockWriter, replayNew.Metadata);
            WriteBlock(writer, blockStream);

            // PresetContainer
            Sections.SerializePresetContainer(blockWriter, replayNew.PresetContainer);
            WriteBlock(writer, blockStream);

            writer.Write(replayNew.PlayerCount);
            foreach (var playerName in replayNew.PlayerNames)
            {
                writer.Write(playerName);
            }

            foreach (var frame in replayNew.Frames)
            {
                Sections.SerializeFrame(blockWriter, frame);
                WriteBlock(writer, blockStream);
            }
        }

        public static ReplayNew? DeserializeReplay(BinaryReader reader, int version = 0)
        {
            var replay = new ReplayNew();

            var header = Sections.DeserializeHeader(reader, version);
            if (header == null)
            {
                return null;
            }

            var position = reader.BaseStream.Position;
            var sha = SHA1.Create();
            var hash = sha.ComputeHash(reader.BaseStream);
            var computedChecksum = HashWrapper.Create(hash);

            if(!header.Value.ReplayChecksum.Equals(computedChecksum))
            {
                return null;
            }

            // Metadata
            var metadataLength = ReadBlockLength(reader);
            replay.Metadata = Sections.DeserializeMetadata(reader, version);

            // PresetContainer
            var presetContainerLength = ReadBlockLength(reader);
            replay.PresetContainer = Sections.DeserializePresetContainer(reader, version);

            int playerCount = reader.ReadInt32();
            var playerNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerNames[i] = reader.ReadString();
            }

            replay.Frames = new ReplayFrame[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                replay.Frames[i] = Sections.DeserializeFrame(reader, version);
            }

            return replay;
        }

        #endregion

        private static int WriteBlock(BinaryWriter writer, Stream blockStream)
        {
            var length = WriteBlockLength(writer, blockStream);
            blockStream.CopyTo(writer.BaseStream, length);
            blockStream.Position = 0;

            return length;
        }

        private static int WriteBlockLength(BinaryWriter writer, Stream blockStream)
        {
            var length = WriteBlockLength(writer, (int) blockStream.Position);
            blockStream.Position = 0;

            return length;
        }

        private static int WriteBlockLength(BinaryWriter writer, int length)
        {
            const int maxLength = 0x3FFF_FFFF;
            if(length > maxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Block length is too large");
            }

            // Split the length into 2 shorts
            ushort low = (ushort) (length & 0xFFFF);
            ushort high = (ushort) ((length >> 16) & 0xFFFF);

            // If the low byte has the MSB set, we need to write 2 shorts instead of 1
            if ((low & 0b1000_0000) == 1)
            {
                // Set the MSB to 1 to indicate that the length is stored in 2 bytes
                high |= 0x8000;
                writer.Write(high);
            }

            writer.Write(low);

            return length;
        }

        private static int ReadBlockLength(BinaryReader reader)
        {
            Span<ushort> data = stackalloc ushort[2] { 0x00, 0x00 };

            ref ushort low = ref data[0];
            ref ushort high = ref data[1];

            low = reader.ReadUInt16();

            // If the first (high) byte is negative (MSB is 1), it means that the length is stored in 2 bytes
            if ((data[0] & 0x8000) == 1)
            {
                // Take out MSB to make it positive
                const ushort msb = 0x8000;
                low &= unchecked((ushort) ~msb);

                // Read the second byte
                high = reader.ReadUInt16();

                // Move the first byte to the second byte
                // Don't need to remove the MSB since it's positive
                //(low, high) = (high, low);
            }

            unsafe
            {
                // Return the 2 shorts as a single int
                fixed (void* dataPtr = &low)
                {
                    return *(int*) dataPtr;
                }
            }
        }
    }
}