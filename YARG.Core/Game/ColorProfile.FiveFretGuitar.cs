using System.Drawing;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FiveFretGuitarColors
        {
            #region Frets

            public Color OpenFret   = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // 255, 200,   0, 255
            public Color GreenFret  = Color.FromArgb(0xFF, 0x54, 0x98, 0x03); // 255, 84,  152,   3
            public Color RedFret    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x07); // 255, 255,   0,   7
            public Color YellowFret = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // 255, 255, 233,   0
            public Color BlueFret   = Color.FromArgb(0xFF, 0x00, 0x72, 0x98); // 255,   0, 114, 152
            public Color OrangeFret = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // 255, 255,  67,   0

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    0 => OpenFret,
                    1 => GreenFret,
                    2 => RedFret,
                    3 => YellowFret,
                    4 => BlueFret,
                    5 => OrangeFret,
                    _ => default
                };
            }

            public Color OpenFretInner   = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // 255, 200,   0, 255
            public Color GreenFretInner  = Color.FromArgb(0xFF, 0x5F, 0xA4, 0x01); // 255, 95,  164,   1
            public Color RedFretInner    = Color.FromArgb(0xFF, 0xC8, 0x00, 0x1D); // 255, 200,   0,  29
            public Color YellowFretInner = Color.FromArgb(0xFF, 0xCA, 0xBE, 0x00); // 255, 202, 190,   0
            public Color BlueFretInner   = Color.FromArgb(0xFF, 0x01, 0x7E, 0xBB); // 255,   1, 126, 187
            public Color OrangeFretInner = Color.FromArgb(0xFF, 0xCA, 0x50, 0x00); // 255, 202,  80,   0

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    0 => OpenFretInner,
                    1 => GreenFretInner,
                    2 => RedFretInner,
                    3 => YellowFretInner,
                    4 => BlueFretInner,
                    5 => OrangeFretInner,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color OpenNote   = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // 255, 200,   0, 255
            public Color GreenNote  = Color.FromArgb(0xFF, 0x85, 0xE5, 0x00); // 255, 133, 229,   0
            public Color RedNote    = Color.FromArgb(0xFF, 0xFF, 0x03, 0x00); // 255, 255,   3,   0
            public Color YellowNote = Color.FromArgb(0xFF, 0xFF, 0xE5, 0x00); // 255, 255, 229,   0
            public Color BlueNote   = Color.FromArgb(0xFF, 0x00, 0x5D, 0xFF); // 255,   0,  93, 255
            public Color OrangeNote = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // 255, 255,  67,   0

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    0 => OpenNote,
                    1 => GreenNote,
                    2 => RedNote,
                    3 => YellowNote,
                    4 => BlueNote,
                    5 => OrangeNote,
                    _ => default
                };
            }

            public Color OpenNoteStarPower   = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color GreenNoteStarPower  = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color RedNoteStarPower    = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color YellowNoteStarPower = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color BlueNoteStarPower   = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color OrangeNoteStarPower = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    0 => OpenNoteStarPower,
                    1 => GreenNoteStarPower,
                    2 => RedNoteStarPower,
                    3 => YellowNoteStarPower,
                    4 => BlueNoteStarPower,
                    5 => OrangeNoteStarPower,
                    _ => default
                };
            }

            public Color OpenNoteParticles   = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // 255, 200,   0, 255
            public Color GreenNoteParticles  = Color.FromArgb(0xFF, 0x94, 0xFF, 0x00); // 255, 148, 255,   0
            public Color RedNoteParticles    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x26); // 255, 255,   0,  38
            public Color YellowNoteParticles = Color.FromArgb(0xFF, 0xFF, 0xF0, 0x00); // 255, 255, 240,   0
            public Color BlueNoteParticles   = Color.FromArgb(0xFF, 0x00, 0xAB, 0xFF); // 255,   0, 171, 255
            public Color OrangeNoteParticles = Color.FromArgb(0xFF, 0xFF, 0x65, 0x00); // 255, 255, 101,   0

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteParticleColor(int index)
            {
                return index switch
                {
                    0 => OpenNoteParticles,
                    1 => GreenNoteParticles,
                    2 => RedNoteParticles,
                    3 => YellowNoteParticles,
                    4 => BlueNoteParticles,
                    5 => OrangeNoteParticles,
                    _ => default
                };
            }

            #endregion
        }
    }
}