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

        private static readonly Color DefaultStarpower  = Color.White; // #FFFFFF

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

        public static readonly List<ColorProfile> Defaults = new()
        {
            Default,
            CircularDefault
        };
    }
}