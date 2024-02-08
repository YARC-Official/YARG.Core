using System;
using System.Drawing;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Extensions
{
    public static class BinaryWriterExtensions
    {
        public static void Write(this IBinaryDataWriter writer, Color color)
        {
            writer.Write(color.ToArgb());
        }

        public static Color ReadColor(this IBinaryDataReader reader)
        {
            int argb = reader.ReadInt32();
            return Color.FromArgb(argb);
        }

        public static void Write(this IBinaryDataWriter writer, Guid guid)
        {
            Span<byte> span = stackalloc byte[16];
            if (!guid.TryWriteBytes(span))
            {
                throw new InvalidOperationException("Failed to write GUID bytes.");
            }

            writer.Write(span);
        }

        public static Guid ReadGuid(this IBinaryDataReader reader)
        {
            Span<byte> span = stackalloc byte[16];
            if (reader.Read(span) != span.Length)
            {
                throw new EndOfStreamException("Failed to read GUID, ran out of bytes!");
            }

            return new Guid(span);
        }
    }
}