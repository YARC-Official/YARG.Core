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

            public Color BackgroundBaseColor1 = Color.FromArgb(255, 15, 15, 15);
            public Color BackgroundBaseColor2 = Color.FromArgb(38, 75, 75, 75);
            public Color BackgroundBaseColor3 = Color.FromArgb(0, 255, 255, 255);               // Possibly Unused
            public Color BackgroundPatternColor = Color.FromArgb(255, 87, 87, 87);

            public Color BackgroundGrooveBaseColor1 = Color.FromArgb(255, 0, 9, 51);
            public Color BackgroundGrooveBaseColor2 = Color.FromArgb(38, 35, 51, 196);
            public Color BackgroundGrooveBaseColor3 = Color.FromArgb(0, 255, 255, 255);         // Possibly Unused
            public Color BackgroundGroovePatternColor = Color.FromArgb(255, 44, 73, 158);

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                StarPowerColor = reader.ReadColor();
                BackgroundBaseColor1 = reader.ReadColor();
                BackgroundBaseColor2 = reader.ReadColor();
                BackgroundBaseColor3 = reader.ReadColor();
                BackgroundPatternColor = reader.ReadColor();
                BackgroundGrooveBaseColor1 = reader.ReadColor();
                BackgroundGrooveBaseColor2 = reader.ReadColor();
                BackgroundGrooveBaseColor3 = reader.ReadColor();
                BackgroundGroovePatternColor = reader.ReadColor();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(StarPowerColor);
                writer.Write(BackgroundBaseColor1);
                writer.Write(BackgroundBaseColor2);
                writer.Write(BackgroundBaseColor3);
                writer.Write(BackgroundPatternColor);
                writer.Write(BackgroundGrooveBaseColor1);
                writer.Write(BackgroundGrooveBaseColor2);
                writer.Write(BackgroundGrooveBaseColor3);
                writer.Write(BackgroundGroovePatternColor);
            }

            public CommonColors Copy()
            {
                return (CommonColors) MemberwiseClone();
            }

            public Color[] BackgroundBaseColors => new[] { BackgroundBaseColor1, BackgroundBaseColor2, BackgroundBaseColor3, BackgroundPatternColor };
            public Color[] BackgroundGrooveBaseColors => new[] { BackgroundGrooveBaseColor1, BackgroundGrooveBaseColor2, BackgroundGrooveBaseColor3, BackgroundGroovePatternColor };
        }
    }
}
