using System.Collections.Generic;

namespace YARG.Core.Game
{
    public partial class EnginePreset
    {
        public static EnginePreset Default = new("Default", true);

        public static EnginePreset Casual = new("Casual", true)
        {
            FiveFretGuitar =
            {
                StrumLeniency = 0.07,
                StrumLeniencySmall = 0.03,
                AntiGhosting = false,
                InfiniteFrontEnd = true
            }
        };

        public static EnginePreset Precision = new("Precision", true)
        {
            FiveFretGuitar =
            {
                StrumLeniency = 0.05,
                StrumLeniencySmall = 0.02,
                HitWindow =
                {
                    MaxWindow = 0.15,
                    MinWindow = 0.13,
                    IsDynamic = true,
                }
            },
            Drums =
            {
                HitWindow =
                {
                    MaxWindow = 0.15,
                    MinWindow = 0.13,
                    IsDynamic = true,
                }
            }
        };

        public static readonly List<EnginePreset> Defaults = new()
        {
            Default,
            Casual,
            Precision
        };
    }
}