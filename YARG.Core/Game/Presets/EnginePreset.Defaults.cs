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
                AntiGhosting = false,
                InfiniteFrontEnd = true,
                StrumLeniency = 0.07,
                StrumLeniencySmall = 0.035
            }
        };

        public static EnginePreset Precision = new("Precision", true)
        {
            FiveFretGuitar =
            {
                StrumLeniency = 0.04,
                StrumLeniencySmall = 0.02,
                HitWindow =
                {
                    MaxWindow = 0.13,
                    MinWindow = 0.04,
                    IsDynamic = true,
                }
            },
            Drums =
            {
                HitWindow =
                {
                    MaxWindow = 0.13,
                    MinWindow = 0.04,
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