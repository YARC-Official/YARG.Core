using System.Drawing;
using System.IO;
using YARG.Core.Game.Settings;

namespace YARG.Core.Game
{
    public partial class HighwayPreset : BasePreset
    {
        [SettingType(SettingType.Special)]
        public Color StarPowerColor = Color.FromArgb(255, 255, 152, 0);

        [SettingType(SettingType.Special)]
        public Color BackgroundBaseColor1 = Color.FromArgb(255, 15, 15, 15);
        [SettingType(SettingType.Special)]
        public Color BackgroundBaseColor2 = Color.FromArgb(38, 75, 75, 75);

        public Color BackgroundBaseColor3 = Color.FromArgb(0, 255, 255, 255);               // Currently Unused
        [SettingType(SettingType.Special)]
        public Color BackgroundPatternColor = Color.FromArgb(255, 87, 87, 87);

        [SettingType(SettingType.Special)]
        public Color BackgroundGrooveBaseColor1 = Color.FromArgb(255, 0, 9, 51);
        [SettingType(SettingType.Special)]
        public Color BackgroundGrooveBaseColor2 = Color.FromArgb(38, 35, 51, 196);

        public Color BackgroundGrooveBaseColor3 = Color.FromArgb(0, 255, 255, 255);         // Currently Unused
        [SettingType(SettingType.Special)]
        public Color BackgroundGroovePatternColor = Color.FromArgb(255, 44, 73, 158);

        [SettingType(SettingType.Slider)]
        [SettingRange(0.8f, 1.2f)]
        public float NoteHeight = 1f;

        [SettingType(SettingType.FileInfo)]
        public FileInfo? BackgroundImage;
        [SettingType(SettingType.FileInfo)]
        public FileInfo? SideImage;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 0.05f)]
        public float BaseWaviness = 0.01f;
        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 0.05f)]
        public float SideWaviness = 0.01f;

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
                BackgroundGroovePatternColor = BackgroundGroovePatternColor,
                BackgroundImage = BackgroundImage,
                SideImage = SideImage,
                NoteHeight = NoteHeight,
                BaseWaviness = BaseWaviness,
                SideWaviness = SideWaviness
            };
        }

        public string? GetExtraContentFolder()
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(Path);
            var directory = System.IO.Path.GetDirectoryName(Path);

            if (baseName == null || directory == null)
            {
                return null;
            }

            return System.IO.Path.Combine(directory, baseName);
        }
    }
}
