using System.Collections.Generic;
using System.Drawing;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        #region Default Colors

        private static readonly Color DefaultPurpleFret = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenFret  = Color.FromArgb(0xFF, 0x54, 0x98, 0x03); // #549803
        private static readonly Color DefaultRedFret    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x07); // #ff0007
        private static readonly Color DefaultYellowFret = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #ffe900
        private static readonly Color DefaultBlueFret   = Color.FromArgb(0xFF, 0x00, 0x72, 0x98); // #007298
        private static readonly Color DefaultOrangeFret = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // #ff4300

        private static readonly Color DefaultPurpleFretInner = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenFretInner  = Color.FromArgb(0xFF, 0x5F, 0xA4, 0x01); // #5fa401
        private static readonly Color DefaultRedFretInner    = Color.FromArgb(0xFF, 0xC8, 0x00, 0x1D); // #c8001d
        private static readonly Color DefaultYellowFretInner = Color.FromArgb(0xFF, 0xCA, 0xBE, 0x00); // #cabe00
        private static readonly Color DefaultBlueFretInner   = Color.FromArgb(0xFF, 0x01, 0x7E, 0xBB); // #017ebb
        private static readonly Color DefaultOrangeFretInner = Color.FromArgb(0xFF, 0xCA, 0x50, 0x00); // #ca5000

        private static readonly Color DefaultPurpleNote = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenNote  = Color.FromArgb(0xFF, 0x85, 0xE5, 0x00); // #85e500
        private static readonly Color DefaultRedNote    = Color.FromArgb(0xFF, 0xFF, 0x03, 0x00); // #ff0300
        private static readonly Color DefaultYellowNote = Color.FromArgb(0xFF, 0xFF, 0xE5, 0x00); // #ffe500
        private static readonly Color DefaultBlueNote   = Color.FromArgb(0xFF, 0x00, 0x5D, 0xFF); // #005dff
        private static readonly Color DefaultOrangeNote = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // #ff4300

        private static readonly Color DefaultStarpower  = Color.White; // #ffffff

        private static readonly Color DefaultPurpleParticles = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenParticles  = Color.FromArgb(0xFF, 0x94, 0xFF, 0x00); // #94ff00
        private static readonly Color DefaultRedParticles    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x26); // #ff0026
        private static readonly Color DefaultYellowParticles = Color.FromArgb(0xFF, 0xFF, 0xF0, 0x00); // #fff000
        private static readonly Color DefaultBlueParticles   = Color.FromArgb(0xFF, 0x00, 0xAB, 0xFF); // #00abff
        private static readonly Color DefaultOrangeParticles = Color.FromArgb(0xFF, 0xFF, 0x65, 0x00); // #ff6500

        #endregion

        #region Circular Colors

        private static readonly Color CircularGreenFret  = Color.FromArgb(0xFF, 0x00, 0x59, 0x00); // #005900
        private static readonly Color CircularRedFret    = Color.FromArgb(0xFF, 0x9A, 0x00, 0x00); // #9a0000
        private static readonly Color CircularYellowFret = Color.FromArgb(0xFF, 0xFF, 0xDE, 0x00); // #ffde00
        private static readonly Color CircularBlueFret   = Color.FromArgb(0xFF, 0x00, 0x52, 0xB8); // #0052b8
        private static readonly Color CircularOrangeFret = Color.FromArgb(0xFF, 0xE9, 0x73, 0x00); // #e97300

        private static readonly Color CircularPurpleNote = Color.FromArgb(0xFF, 0x81, 0x00, 0xB1); // #8100b1
        private static readonly Color CircularGreenNote  = Color.FromArgb(0xFF, 0x00, 0x6E, 0x08); // #006e08
        private static readonly Color CircularRedNote    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x00); // #ff0000
        private static readonly Color CircularYellowNote = Color.FromArgb(0xFF, 0xFF, 0xE4, 0x00); // #ffe400
        private static readonly Color CircularBlueNote   = Color.FromArgb(0xFF, 0x00, 0x46, 0xE6); // #0046e6
        private static readonly Color CircularOrangeNote = Color.FromArgb(0xFF, 0x8D, 0x28, 0x00); // #8d2800

        private static readonly Color CircularStarpower = Color.FromArgb(0xFF, 0x13, 0xD9, 0xEA); // #13D9EA

        #endregion

        public static ColorProfile Default = new("Default", true);

        public static readonly List<ColorProfile> Defaults = new()
        {
            Default,
            new ColorProfile("Circular", true)
            {
                FiveFretGuitar = new()
                {
                    OpenFret   = DefaultPurpleFret,
                    GreenFret  = CircularGreenFret,
                    RedFret    = CircularRedFret,
                    YellowFret = CircularYellowFret,
                    BlueFret   = CircularBlueFret,
                    OrangeFret = CircularOrangeFret,

                    OpenNote   = CircularPurpleNote,
                    GreenNote  = CircularGreenNote,
                    RedNote    = CircularRedNote,
                    YellowNote = CircularYellowNote,
                    BlueNote   = CircularBlueNote,
                    OrangeNote = CircularOrangeNote,

                    OpenNoteStarPower   = CircularStarpower,
                    GreenNoteStarPower  = CircularStarpower,
                    RedNoteStarPower    = CircularStarpower,
                    YellowNoteStarPower = CircularStarpower,
                    BlueNoteStarPower   = CircularStarpower,
                    OrangeNoteStarPower = CircularStarpower,
                }
            }
        };
    }
}