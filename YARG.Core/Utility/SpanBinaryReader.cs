using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.Utility
{
    public ref struct SpanBinaryReader
    {
        public int Position { get; private set; }

        public int Length => Data.Length;

        public readonly ReadOnlySpan<byte> Data;

        public SpanBinaryReader(ReadOnlySpan<byte> data)
        {
            Data = data;
            Position = 0;
        }

        public byte ReadByte()
        {
            var value = Data[Position];
            Position += 1;
            return value;
        }

        public ushort ReadUInt16()
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(Data[Position..]);
            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(Data[Position..]);
            Position += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(Data[Position..]);
            Position += 8;
            return value;
        }

        public short ReadInt16()
        {
            var value = BinaryPrimitives.ReadInt16LittleEndian(Data[Position..]);
            Position += 2;
            return value;
        }

        public int ReadInt32()
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(Data[Position..]);
            Position += 4;
            return value;
        }

        public long ReadInt64()
        {
            var value = BinaryPrimitives.ReadInt64LittleEndian(Data[Position..]);
            Position += 8;
            return value;
        }

        public float ReadSingle()
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(Data[Position..]);
            float result = Unsafe.As<uint, float>(ref value);
            Position += 4;
            return result;
        }

        public double ReadDouble()
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(Data[Position..]);
            double result = Unsafe.As<ulong, double>(ref value);
            Position += 8;
            return result;
        }

        public string ReadString()
        {
            int length = Read7BitEncodedInt();

            var value = Encoding.UTF8.GetString(Data.Slice(Position, length));
            Position += length;

            return value;
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            var value = Data.Slice(Position, length);
            Position += length;
            return value;
        }

        public Guid ReadGuid()
        {
            return new Guid(ReadBytes(16));
        }

        public void Skip(int length)
        {
            Position += length;
        }

        public void Seek(int position)
        {
            Position = position;
        }

        public int Read7BitEncodedInt()
        {
            /*/

             Taken from .NET Runtime source code:
             https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs#L535

             */

            uint result = 0;
            byte byteReadJustNow;

            // Read the integer 7 bits at a time. The high bit
            // of the byte when on means to continue reading more bytes.
            //
            // There are two failure cases: we've read more than 5 bytes,
            // or the fifth byte is about to cause integer overflow.
            // This means that we can read the first 4 bytes without
            // worrying about integer overflow.

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                // ReadByte handles end of stream cases for us.
                byteReadJustNow = ReadByte();
                result |= (byteReadJustNow & 0x7Fu) << shift;

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int)result; // early exit
                }
            }

            // Read the 5th byte. Since we already read 28 bits,
            // the value of this byte must fit within 4 bits (32 - 28),
            // and it must not have the high bit set.

            byteReadJustNow = ReadByte();
            if (byteReadJustNow > 0b_1111u)
            {
                throw new FormatException("Badly formatted 7 bit int");
            }

            result |= (uint)byteReadJustNow << (MaxBytesWithoutOverflow * 7);
            return (int)result;
        }
    }
}