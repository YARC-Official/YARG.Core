using System.Collections.Generic;
using System.Drawing;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        #region Default Colors

        private static readonly Color DefaultPurpleFret = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #C800FF
        private static readonly Color DefaultGreenFret  = Color.FromArgb(0xFF, 0x79, 0xD3, 0x04); // #79D304
        private static readonly Color DefaultRedFret    = Color.FromArgb(0xFF, 0xFF, 0x1D, 0x23); // #FF1D23
        private static readonly Color DefaultYellowFret = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #FFE900
        private static readonly Color DefaultBlueFret   = Color.FromArgb(0xFF, 0x00, 0xBF, 0xFF); // #00BFFF
        private static readonly Color DefaultOrangeFret = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        private static readonly Color DefaultPurpleFretInner = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #C800FF
        private static readonly Color DefaultGreenFretInner  = Color.FromArgb(0xFF, 0x79, 0xD3, 0x04); // #79D304
        private static readonly Color DefaultRedFretInner    = Color.FromArgb(0xFF, 0xFF, 0x1D, 0x23); // #FF1D23
        private static readonly Color DefaultYellowFretInner = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #FFE900
        private static readonly Color DefaultBlueFretInner   = Color.FromArgb(0xFF, 0x00, 0xBF, 0xFF); // #00BFFF
        private static readonly Color DefaultOrangeFretInner = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        private static readonly Color DefaultPurpleNote = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #C800FF
        private static readonly Color DefaultGreenNote  = Color.FromArgb(0xFF, 0x79, 0xD3, 0x04); // #79D304
        private static readonly Color DefaultRedNote    = Color.FromArgb(0xFF, 0xFF, 0x1D, 0x23); // #FF1D23
        private static readonly Color DefaultYellowNote = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #FFE900
        private static readonly Color DefaultBlueNote   = Color.FromArgb(0xFF, 0x00, 0xBF, 0xFF); // #00BFFF
        private static readonly Color DefaultOrangeNote = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        private static readonly Color DefaultStarpower  = Color.White; // #FFFFFF

        private static readonly Color DefaultPurpleParticles = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #C800FF
        private static readonly Color DefaultGreenParticles  = Color.FromArgb(0xFF, 0x79, 0xD3, 0x04); // #79D304
        private static readonly Color DefaultRedParticles    = Color.FromArgb(0xFF, 0xFF, 0x1D, 0x23); // #FF1D23
        private static readonly Color DefaultYellowParticles = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #FFE900
        private static readonly Color DefaultBlueParticles   = Color.FromArgb(0xFF, 0x00, 0xBF, 0xFF); // #00BFFF
        private static readonly Color DefaultOrangeParticles = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #FF8400

        #endregion

        #region Circular Colors

        private static readonly Color CircularPurple = Color.FromArgb(0xFF, 0xBE, 0x0F, 0xFF); // #be0fff
        private static readonly Color CircularGreen  = Color.FromArgb(0xFF, 0x00, 0xC9, 0x0E); // #00c90e
        private static readonly Color CircularRed    = Color.FromArgb(0xFF, 0xC3, 0x00, 0x00); // #c30000
        private static readonly Color CircularYellow = Color.FromArgb(0xFF, 0xF5, 0xD0, 0x00); // #f5d000
        private static readonly Color CircularBlue   = Color.FromArgb(0xFF, 0x00, 0x5C, 0xF5); // #005cf5
        private static readonly Color CircularOrange = Color.FromArgb(0xFF, 0xFF, 0x84, 0x00); // #ff8400

        private static readonly Color CircularStarpower = Color.FromArgb(0xFF, 0x13, 0xD9, 0xEA); // #13d9ea

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