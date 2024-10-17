using System.Collections.Generic;
using System.Drawing;

namespace YARG.Core.Game
{
    public partial class HighwayPreset : BasePreset
    {
        public static HighwayPreset Default = new("Default", true);
        public static HighwayPreset Boring = new("Boring", true)
        {
            StarPowerColor = Color.FromArgb(255, 87, 87, 87),
            BackgroundGrooveBaseColor1 = Color.FromArgb(255, 15, 15, 15),
            BackgroundGrooveBaseColor2 = Color.FromArgb(38, 75, 75, 75)
        };

        public static readonly List<HighwayPreset> Defaults = new()
        {
            Default,
            Boring,
        };
    }
}
