using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    public struct RBAudio<TType>
        where TType : unmanaged
    {
        public TType[]? Track;
        public TType[]? Drums;
        public TType[]? Bass;
        public TType[]? Guitar;
        public TType[]? Keys;
        public TType[]? Vocals;
        public TType[]? Crowd;

        public RBAudio(UnmanagedMemoryStream stream)
        {
            Track = ReadArray(stream);
            Drums = ReadArray(stream);
            Bass = ReadArray(stream);
            Guitar = ReadArray(stream);
            Keys = ReadArray(stream);
            Vocals = ReadArray(stream);
            Crowd = ReadArray(stream);
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            WriteArray(Track, writer);
            WriteArray(Drums, writer);
            WriteArray(Bass, writer);
            WriteArray(Guitar, writer);
            WriteArray(Keys, writer);
            WriteArray(Vocals, writer);
            WriteArray(Crowd, writer);
        }

        public static void WriteArray(in TType[]? values, BinaryWriter writer)
        {
            if (values != null)
            {
                writer.Write(values.Length);
                unsafe
                {
                    fixed (TType* ptr = values)
                    {
                        var span = new ReadOnlySpan<byte>(ptr, values.Length * sizeof(TType));
                        writer.Write(span);
                    }
                }
            }
            else
            {
                writer.Write(0);
            }
        }

        public static TType[]? ReadArray(UnmanagedMemoryStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return null;
            }

            var values = new TType[length];
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    var span = new Span<byte>(ptr, values.Length * sizeof(TType));
                    stream.Read(span);
                }
            }
            return values;
        }
    }
}
