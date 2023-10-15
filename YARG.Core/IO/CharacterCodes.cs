using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.IO
{
    /// <summary>
    /// A four-byte identifier ("four-character code") used to identify data formats.
    /// </summary>
    /// <remarks>
    /// These are read and written in big-endian, so that the characters used are
    /// human-readable in a hex editor, for example.
    /// </remarks>
    public readonly struct FourCC : IBinarySerializable
    {
        private readonly uint _code;

        private FourCC(uint code)
        {
            _code = code;
        }

        public FourCC(char a, char b, char c, char d)
            : this((byte) a, (byte) b, (byte) c, (byte) d) {}

        public FourCC(byte a, byte b, byte c, byte d)
        {
            _code = ((uint) a << 24) | ((uint) b << 16) | ((uint) c << 8) | d;
        }

        public FourCC(ReadOnlySpan<byte> data)
        {
            _code = BinaryPrimitives.ReadUInt32BigEndian(data);
        }

        public static FourCC Read(Stream stream) => new(stream.ReadUInt32BE());
        public static FourCC Read(BinaryReader reader) => new(reader.ReadUInt32BE());
        public static FourCC Read(YARGBinaryReader reader) => new(reader.ReadUInt32(Endianness.BigEndian));

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteUInt32BE(_code);
        }

        [Obsolete("FourCC is a readonly struct, use the Read static method instead.", true)]
        public void Deserialize(BinaryReader reader, int version = 0)
            => throw new InvalidOperationException("FourCC is a readonly struct, use the Read static method instead.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FourCC left, FourCC right) => left._code == right._code;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FourCC left, FourCC right) => left._code != right._code;

        public bool Equals(FourCC other) => this == other;
        public override bool Equals(object obj) => obj is FourCC cc && Equals(cc);
        public override int GetHashCode() => _code.GetHashCode();

        public override string ToString()
        {
            char a = (char) ((_code >> 24) & 0xFF);
            char b = (char) ((_code >> 16) & 0xFF);
            char c = (char) ((_code >> 8) & 0xFF);
            char d = (char) (_code & 0xFF);
            return $"{a}{b}{c}{d}";
        }
    }

    /// <summary>
    /// An eight-byte identifier ("eight-character code") used to identify data formats.
    /// </summary>
    /// <remarks>
    /// These are read and written in big-endian, so that the characters used are
    /// human-readable in a hex editor, for example.
    /// </remarks>
    public readonly struct EightCC : IBinarySerializable
    {
        private readonly ulong _code;

        private EightCC(ulong code)
        {
            _code = code;
        }

        public EightCC(char a, char b, char c, char d, char e, char f, char g, char h)
            : this((byte) a, (byte) b, (byte) c, (byte) d, (byte) e, (byte) f, (byte) g, (byte) h) {}

        public EightCC(byte a, byte b, byte c, byte d, byte e, byte f, byte g, byte h)
        {
            _code = ((ulong) a << 56) | ((ulong) b << 48) | ((ulong) c << 40) | ((ulong) d << 32) |
                ((ulong) e << 24) | ((ulong) f << 16) | ((ulong) g << 8) | h;
        }

        public EightCC(ReadOnlySpan<byte> data)
        {
            _code = BinaryPrimitives.ReadUInt64BigEndian(data);
        }

        public static EightCC Read(Stream stream) => new(stream.ReadUInt64BE());
        public static EightCC Read(BinaryReader reader) => new(reader.ReadUInt64BE());
        public static EightCC Read(YARGBinaryReader reader) => new(reader.ReadUInt64(Endianness.BigEndian));

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteUInt64BE(_code);
        }

        [Obsolete("EightCC is a readonly struct, use the Read static method instead.", true)]
        public void Deserialize(BinaryReader reader, int version = 0)
            => throw new InvalidOperationException("EightCC is a readonly struct, use the Read static method instead.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(EightCC left, EightCC right) => left._code == right._code;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(EightCC left, EightCC right) => left._code != right._code;

        public bool Equals(EightCC other) => this == other;
        public override bool Equals(object obj) => obj is EightCC cc && Equals(cc);
        public override int GetHashCode() => _code.GetHashCode();

        public override string ToString()
        {
            char a = (char) ((_code >> 56) & 0xFF);
            char b = (char) ((_code >> 48) & 0xFF);
            char c = (char) ((_code >> 40) & 0xFF);
            char d = (char) ((_code >> 32) & 0xFF);
            char e = (char) ((_code >> 24) & 0xFF);
            char f = (char) ((_code >> 16) & 0xFF);
            char g = (char) ((_code >> 8) & 0xFF);
            char h = (char) (_code & 0xFF);
            return $"{a}{b}{c}{d}{e}{f}{g}{h}";
        }
    }
}