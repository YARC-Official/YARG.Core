using System.Collections.Generic;
using System.Drawing;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        #region Default Colors

        private static readonly Color DefaultPurple = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #C800FF
        private static readonly Color DefaultGreen  = Color.FromArgb(0xFF, 0x79, 0xD3, 0x04); // #79D304
        private static readonly Color DefaultRed    = Color.FromArgb(0xFF, 0xFF, 0x1D, 0x23); // #FF1D23
        private static readonly Color DefaultYellow = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #FFE900
        private static readonly Color DefaultBlue   = Color.FromArgb(0xFF, 0x00, 0xBF, 0xFF); // #00BFFF
        private static readonly Color DefaultOrange = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        // By default, use these colors for notes only, not fret coloring or particles
        private static readonly Color DefaultRedCymbal    = Color.FromArgb(0xFF, 0xF0, 0x20, 0x40); // #F02040
        private static readonly Color DefaultYellowCymbal = Color.FromArgb(0xFF, 0xFF, 0xD0, 0x10); // #FFD010
        private static readonly Color DefaultBlueCymbal   = Color.FromArgb(0xFF, 0x20, 0x90, 0xFF); // #2090FF
        private static readonly Color DefaultGreenCymbal  = Color.FromArgb(0xFF, 0xA0, 0xD0, 0x10); // #A0D010

        private static readonly Color DefaultStarpower = Color.White; // #FFFFFF

        private static readonly Color DefaultMetal          = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // #FFFFFF
        private static readonly Color DefaultMetalStarPower = Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00); // #FFD700

        private static readonly Color DefaultMiss = Color.FromArgb(0xFF, 0x90, 0x90, 0x90); // #909090

        #endregion

        #region Circular Colors

        private static readonly Color CircularPurple = Color.FromArgb(0xFF, 0xBE, 0x0F, 0xFF); // #BE0FFF
        private static readonly Color CircularGreen  = Color.FromArgb(0xFF, 0x00, 0xC9, 0x0E); // #00C90E
        private static readonly Color CircularRed    = Color.FromArgb(0xFF, 0xC3, 0x00, 0x00); // #C30000
        private static readonly Color CircularYellow = Color.FromArgb(0xFF, 0xF5, 0xD0, 0x00); // #F5D000
        private static readonly Color CircularBlue   = Color.FromArgb(0xFF, 0x00, 0x5C, 0xF5); // #005CF5
        private static readonly Color CircularOrange = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        private static readonly Color CircularStarpower = Color.FromArgb(0xFF, 0x13, 0xD9, 0xEA); // #13D9EA

        #endregion

        #region April Fools Colors

        private static readonly Color AprilFoolsGreen  = Color.FromArgb(0xFF, 0x24, 0xB9, 0x00); // #24B900
        private static readonly Color AprilFoolsRed    = Color.FromArgb(0xFF, 0xD1, 0x13, 0x00); // #D11300
        private static readonly Color AprilFoolsYellow = Color.FromArgb(0xFF, 0xD1, 0xA7, 0x00); // #D1A700
        private static readonly Color AprilFoolsBlue   = Color.FromArgb(0xFF, 0x00, 0x1A, 0xDC); // #001ADC
        private static readonly Color AprilFoolsPurple = Color.FromArgb(0xFF, 0xEB, 0x00, 0xD1); // #EB00D1

        #endregion

        public static ColorProfile Default = new("Default", true);

        public static ColorProfile CircularDefault = new("Circular", true)
        {
            FiveFretGuitar = new FiveFretGuitarColors
            {
                OpenFret   = CircularPurple,
                GreenFret  = CircularGreen,
                RedFret    = CircularRed,
                YellowFret = CircularYellow,
                BlueFret   = CircularBlue,
                OrangeFret = CircularOrange,

                OpenFretInner   = CircularPurple,
                GreenFretInner  = CircularGreen,
                RedFretInner    = CircularRed,
                YellowFretInner = CircularYellow,
                BlueFretInner   = CircularBlue,
                OrangeFretInner = CircularOrange,

                OpenNote   = CircularPurple,
                GreenNote  = CircularGreen,
                RedNote    = CircularRed,
                YellowNote = CircularYellow,
                BlueNote   = CircularBlue,
                OrangeNote = CircularOrange,

                OpenNoteStarPower   = CircularStarpower,
                GreenNoteStarPower  = CircularStarpower,
                RedNoteStarPower    = CircularStarpower,
                YellowNoteStarPower = CircularStarpower,
                BlueNoteStarPower   = CircularStarpower,
                OrangeNoteStarPower = CircularStarpower,
            }
        };

        public static ColorProfile AprilFoolsDefault = new("YARG on Fire", true)
        {
            FiveFretGuitar = new FiveFretGuitarColors
            {
                OpenFret   = CircularOrange,
                GreenFret  = AprilFoolsGreen,
                RedFret    = AprilFoolsRed,
                YellowFret = AprilFoolsYellow,
                BlueFret   = AprilFoolsBlue,
                OrangeFret = AprilFoolsPurple,

                OpenFretInner   = CircularOrange,
                GreenFretInner  = AprilFoolsGreen,
                RedFretInner    = AprilFoolsRed,
                YellowFretInner = AprilFoolsYellow,
                BlueFretInner   = AprilFoolsBlue,
                OrangeFretInner = AprilFoolsPurple,

                OpenNote   = CircularOrange,
                GreenNote  = AprilFoolsGreen,
                RedNote    = AprilFoolsRed,
                YellowNote = AprilFoolsYellow,
                BlueNote   = AprilFoolsBlue,
                OrangeNote = AprilFoolsPurple,

                OpenNoteStarPower   = CircularStarpower,
                GreenNoteStarPower  = CircularStarpower,
                RedNoteStarPower    = CircularStarpower,
                YellowNoteStarPower = CircularStarpower,
                BlueNoteStarPower   = CircularStarpower,
                OrangeNoteStarPower = CircularStarpower,
            },
            FourLaneDrums = new FourLaneDrumsColors
            {
                KickFret   = AprilFoolsPurple,
                RedFret    = AprilFoolsRed,
                YellowFret = AprilFoolsYellow,
                BlueFret   = AprilFoolsBlue,
                GreenFret  = AprilFoolsGreen,
                YellowCymbalFret = AprilFoolsYellow,
                BlueCymbalFret = AprilFoolsBlue,
                GreenCymbalFret = AprilFoolsGreen,

                KickFretInner   = AprilFoolsPurple,
                RedFretInner    = AprilFoolsRed,
                YellowFretInner = AprilFoolsYellow,
                BlueFretInner   = AprilFoolsBlue,
                GreenFretInner  = AprilFoolsGreen,
                YellowCymbalFretInner = AprilFoolsYellow,
                BlueCymbalFretInner = AprilFoolsBlue,
                GreenCymbalFretInner = AprilFoolsGreen,

                KickNote = AprilFoolsPurple,

                RedDrum    = AprilFoolsRed,
                YellowDrum = AprilFoolsYellow,
                BlueDrum   = AprilFoolsBlue,
                GreenDrum  = AprilFoolsGreen,

                RedCymbal    = AprilFoolsRed,
                YellowCymbal = AprilFoolsYellow,
                BlueCymbal   = AprilFoolsBlue,
                GreenCymbal  = AprilFoolsGreen,

                KickStarpower = CircularStarpower,

                RedDrumStarpower    = CircularStarpower,
                YellowDrumStarpower = CircularStarpower,
                BlueDrumStarpower   = CircularStarpower,
                GreenDrumStarpower  = CircularStarpower,

                RedCymbalStarpower    = CircularStarpower,
                YellowCymbalStarpower = CircularStarpower,
                BlueCymbalStarpower   = CircularStarpower,
                GreenCymbalStarpower  = CircularStarpower,
            },
            FiveLaneDrums = new FiveLaneDrumsColors
            {
                KickFret   = CircularOrange,
                RedFret    = AprilFoolsRed,
                YellowFret = AprilFoolsYellow,
                BlueFret   = AprilFoolsBlue,
                OrangeFret = AprilFoolsPurple,
                GreenFret  = AprilFoolsGreen,

                KickFretInner   = CircularOrange,
                RedFretInner    = AprilFoolsRed,
                YellowFretInner = AprilFoolsYellow,
                BlueFretInner   = AprilFoolsBlue,
                OrangeFretInner = AprilFoolsPurple,
                GreenFretInner  = AprilFoolsGreen,

                KickNote   = CircularOrange,
                RedNote    = AprilFoolsRed,
                YellowNote = AprilFoolsYellow,
                BlueNote   = AprilFoolsBlue,
                OrangeNote = AprilFoolsPurple,
                GreenNote  = AprilFoolsGreen,

                KickStarpower   = CircularStarpower,
                RedStarpower    = CircularStarpower,
                YellowStarpower = CircularStarpower,
                BlueStarpower   = CircularStarpower,
                OrangeStarpower = CircularStarpower,
                GreenStarpower  = CircularStarpower,
            }
        };

        public static readonly List<ColorProfile> Defaults = new()
        {
            Default,
            CircularDefault,
            AprilFoolsDefault
        };
    }
}