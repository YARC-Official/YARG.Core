using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Game.Settings;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class HighwayPreset : BasePreset, IBinarySerializable
    {
        [SettingType(SettingType.Special)]
        public Color StarPowerColor = Color.FromArgb(255, 255, 152, 0);

        [SettingType(SettingType.Special)]
        public Color BackgroundBaseColor1 = Color.FromArgb(255, 15, 15, 15);
        [SettingType(SettingType.Special)]
        public Color BackgroundBaseColor2 = Color.FromArgb(38, 75, 75, 75);
        [SettingType(SettingType.Hidden)]
        public Color BackgroundBaseColor3 = Color.FromArgb(0, 255, 255, 255);               // Currently Unused
        [SettingType(SettingType.Special)]
        public Color BackgroundPatternColor = Color.FromArgb(255, 87, 87, 87);

        [SettingType(SettingType.Special)]
        public Color BackgroundGrooveBaseColor1 = Color.FromArgb(255, 0, 9, 51);
        [SettingType(SettingType.Special)]
        public Color BackgroundGrooveBaseColor2 = Color.FromArgb(38, 35, 51, 196);
        [SettingType(SettingType.Hidden)]
        public Color BackgroundGrooveBaseColor3 = Color.FromArgb(0, 255, 255, 255);         // Currently Unused
        [SettingType(SettingType.Special)]
        public Color BackgroundGroovePatternColor = Color.FromArgb(255, 44, 73, 158);

        public Color[] BackgroundBaseColors => new[] { BackgroundBaseColor1, BackgroundBaseColor2, BackgroundBaseColor3, BackgroundPatternColor };
        public Color[] BackgroundGrooveBaseColors => new[] { BackgroundGrooveBaseColor1, BackgroundGrooveBaseColor2, BackgroundGrooveBaseColor3, BackgroundGroovePatternColor };

        public HighwayPreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new HighwayPreset(name)
            {
                StarPowerColor = StarPowerColor,
                BackgroundBaseColor1 = BackgroundBaseColor1,
                BackgroundBaseColor2 = BackgroundBaseColor2,
                BackgroundBaseColor3 = BackgroundBaseColor3,
                BackgroundPatternColor = BackgroundPatternColor,
                BackgroundGrooveBaseColor1 = BackgroundGrooveBaseColor1,
                BackgroundGrooveBaseColor2 = BackgroundGrooveBaseColor2,
                BackgroundGrooveBaseColor3 = BackgroundGrooveBaseColor3,
                BackgroundGroovePatternColor = BackgroundGroovePatternColor
            };
        }

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
    }
}
