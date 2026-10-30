using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace YARG.Core.Chart.Hashing
{
    internal static class Blake3
    {
        private const int OUT_LEN = 32;
        private const int BLOCK_LEN = 64;
        private const int CHUNK_LEN = 1024;

        private const uint CHUNK_START = 1 << 0;
        private const uint CHUNK_END = 1 << 1;
        private const uint PARENT = 1 << 2;
        private const uint ROOT = 1 << 3;

        private static readonly uint[] Iv =
        {
            0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
            0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19,
        };

        private static readonly byte[,] MsgSchedule =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            { 2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8 },
            { 3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1 },
            { 10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6 },
            { 12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4 },
            { 9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7 },
            { 11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13 },
        };

        public static byte[] Hash(ReadOnlySpan<byte> input)
        {
            var key = new uint[8];
            Array.Copy(Iv, key, key.Length);

            var stack = new List<uint[]>();
            ulong chunkCounter = 0;
            var offset = 0;

            while (input.Length - offset > CHUNK_LEN)
            {
                var chunkOutput = ChunkOutput(input.Slice(offset, CHUNK_LEN), key, chunkCounter, 0);
                AddChunkChainingValue(stack, ChainingValue(chunkOutput), chunkCounter + 1, key, 0);
                offset += CHUNK_LEN;
                chunkCounter++;
            }

            var output = ChunkOutput(input.Slice(offset), key, chunkCounter, 0);
            while (stack.Count > 0)
            {
                var left = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                output = ParentOutput(left, ChainingValue(output), key, 0);
            }

            return RootBytes(output);
        }

        private static void AddChunkChainingValue(List<uint[]> stack, uint[] newCv, ulong totalChunks, uint[] key, uint flags)
        {
            while ((totalChunks & 1) == 0)
            {
                newCv = ParentChainingValue(stack[^1], newCv, key, flags);
                stack.RemoveAt(stack.Count - 1);
                totalChunks >>= 1;
            }
            stack.Add(newCv);
        }

        private static Output ChunkOutput(ReadOnlySpan<byte> input, uint[] key, ulong chunkCounter, uint flags)
        {
            var cv = new uint[8];
            Array.Copy(key, cv, cv.Length);

            var blocksCompressed = 0;
            while (input.Length > BLOCK_LEN)
            {
                var blockFlags = flags;
                if (blocksCompressed == 0)
                {
                    blockFlags |= CHUNK_START;
                }

                cv = Compress(cv, WordsFromBlock(input.Slice(0, BLOCK_LEN)), chunkCounter, BLOCK_LEN, blockFlags, false);
                blocksCompressed++;
                input = input.Slice(BLOCK_LEN);
            }

            var outputFlags = flags | CHUNK_END;
            if (blocksCompressed == 0)
            {
                outputFlags |= CHUNK_START;
            }

            return new Output(cv, WordsFromBlock(input), chunkCounter, (uint) input.Length, outputFlags);
        }

        private static Output ParentOutput(uint[] leftChildCv, uint[] rightChildCv, uint[] key, uint flags)
        {
            var blockWords = new uint[16];
            Array.Copy(leftChildCv, 0, blockWords, 0, 8);
            Array.Copy(rightChildCv, 0, blockWords, 8, 8);
            return new Output(key, blockWords, 0, BLOCK_LEN, flags | PARENT);
        }

        private static uint[] ParentChainingValue(uint[] leftChildCv, uint[] rightChildCv, uint[] key, uint flags)
        {
            return ChainingValue(ParentOutput(leftChildCv, rightChildCv, key, flags));
        }

        private static uint[] ChainingValue(Output output)
        {
            return Compress(output.InputChainingValue, output.BlockWords, output.Counter, output.BlockLength, output.Flags, false);
        }

        private static byte[] RootBytes(Output output)
        {
            var words = Compress(output.InputChainingValue, output.BlockWords, 0, output.BlockLength, output.Flags | ROOT, true);
            var bytes = new byte[OUT_LEN];
            for (var i = 0; i < OUT_LEN / sizeof(uint); i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), words[i]);
            }
            return bytes;
        }

        private static uint[] WordsFromBlock(ReadOnlySpan<byte> block)
        {
            var words = new uint[16];
            var fullWords = block.Length / sizeof(uint);
            for (var i = 0; i < fullWords; i++)
            {
                words[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(i * sizeof(uint), sizeof(uint)));
            }

            var remaining = block.Length % sizeof(uint);
            if (remaining > 0)
            {
                Span<byte> padded = stackalloc byte[sizeof(uint)];
                block.Slice(fullWords * sizeof(uint), remaining).CopyTo(padded);
                words[fullWords] = BinaryPrimitives.ReadUInt32LittleEndian(padded);
            }
            return words;
        }

        private static uint[] Compress(uint[] chainingValue, uint[] blockWords, ulong counter, uint blockLength, uint flags, bool fullOutput)
        {
            var state = new uint[16];
            Array.Copy(chainingValue, state, 8);
            state[8] = Iv[0];
            state[9] = Iv[1];
            state[10] = Iv[2];
            state[11] = Iv[3];
            state[12] = (uint) counter;
            state[13] = (uint) (counter >> 32);
            state[14] = blockLength;
            state[15] = flags;

            for (var round = 0; round < 7; round++)
            {
                Round(state, blockWords, round);
            }

            var output = new uint[fullOutput ? 16 : 8];
            for (var i = 0; i < 8; i++)
            {
                output[i] = state[i] ^ state[i + 8];
                if (fullOutput)
                {
                    output[i + 8] = state[i + 8] ^ chainingValue[i];
                }
            }
            return output;
        }

        private static void Round(uint[] state, uint[] message, int round)
        {
            G(state, 0, 4, 8, 12, message[MsgSchedule[round, 0]], message[MsgSchedule[round, 1]]);
            G(state, 1, 5, 9, 13, message[MsgSchedule[round, 2]], message[MsgSchedule[round, 3]]);
            G(state, 2, 6, 10, 14, message[MsgSchedule[round, 4]], message[MsgSchedule[round, 5]]);
            G(state, 3, 7, 11, 15, message[MsgSchedule[round, 6]], message[MsgSchedule[round, 7]]);
            G(state, 0, 5, 10, 15, message[MsgSchedule[round, 8]], message[MsgSchedule[round, 9]]);
            G(state, 1, 6, 11, 12, message[MsgSchedule[round, 10]], message[MsgSchedule[round, 11]]);
            G(state, 2, 7, 8, 13, message[MsgSchedule[round, 12]], message[MsgSchedule[round, 13]]);
            G(state, 3, 4, 9, 14, message[MsgSchedule[round, 14]], message[MsgSchedule[round, 15]]);
        }

        private static void G(uint[] state, int a, int b, int c, int d, uint x, uint y)
        {
            state[a] = unchecked(state[a] + state[b] + x);
            state[d] = RotateRight(state[d] ^ state[a], 16);
            state[c] = unchecked(state[c] + state[d]);
            state[b] = RotateRight(state[b] ^ state[c], 12);
            state[a] = unchecked(state[a] + state[b] + y);
            state[d] = RotateRight(state[d] ^ state[a], 8);
            state[c] = unchecked(state[c] + state[d]);
            state[b] = RotateRight(state[b] ^ state[c], 7);
        }

        private static uint RotateRight(uint value, int count)
        {
            return (value >> count) | (value << (32 - count));
        }

        private readonly struct Output
        {
            public readonly uint[] InputChainingValue;
            public readonly uint[] BlockWords;
            public readonly ulong Counter;
            public readonly uint BlockLength;
            public readonly uint Flags;

            public Output(uint[] inputChainingValue, uint[] blockWords, ulong counter, uint blockLength, uint flags)
            {
                InputChainingValue = inputChainingValue;
                BlockWords = blockWords;
                Counter = counter;
                BlockLength = blockLength;
                Flags = flags;
            }
        }
    }
}