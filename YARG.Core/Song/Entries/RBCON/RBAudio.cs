using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        public RBAudio(BinaryReader reader)
        {
            Track = ReadArray(reader);
            Drums = ReadArray(reader);
            Bass = ReadArray(reader);
            Guitar = ReadArray(reader);
            Keys = ReadArray(reader);
            Vocals = ReadArray(reader);
            Crowd = ReadArray(reader);
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

        public static TType[]? ReadArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
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
                    reader.Read(span);
                }
            }
            return values;
        }
    }
}
