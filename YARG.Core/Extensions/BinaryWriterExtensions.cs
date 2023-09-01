using System.Drawing;
using System.IO;

namespace YARG.Core.Extensions
{
    public static class BinaryWriterExtensions
    {
        public static void Write(this BinaryWriter writer, Color color)
        {
            writer.Write(color.ToArgb());
        }

        public static Color ReadColor(this BinaryReader reader)
        {
            int argb = reader.ReadInt32();
            return Color.FromArgb(argb);
        }
    }
}