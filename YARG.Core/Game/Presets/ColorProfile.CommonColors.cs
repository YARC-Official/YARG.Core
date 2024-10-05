using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class CommonColors : IBinarySerializable
        {
            public Color StarPowerColor = Color.FromArgb(255,255,152,0);
            public Color GrooveColor1 = Color.FromArgb(255, 0, 9, 51);
            public Color GrooveColor2 = Color.FromArgb(38, 35, 51, 196);
            public Color GrooveColor3 = Color.FromArgb(0, 255, 255, 255);
            public Color GrooveColor4 = Color.FromArgb(255, 44, 73, 158);

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                StarPowerColor = reader.ReadColor();
                GrooveColor1 = reader.ReadColor();
                GrooveColor2 = reader.ReadColor();
                GrooveColor3 = reader.ReadColor();
                GrooveColor4 = reader.ReadColor();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(StarPowerColor);
                writer.Write(GrooveColor1);
                writer.Write(GrooveColor2);
                writer.Write(GrooveColor3);
                writer.Write(GrooveColor4);
            }

            public CommonColors Copy()
            {
                return (CommonColors) this.MemberwiseClone();
            }
        }
    }
}
