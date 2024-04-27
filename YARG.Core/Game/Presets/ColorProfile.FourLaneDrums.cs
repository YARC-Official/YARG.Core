using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public struct FourLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            public static readonly FourLaneDrumsColors Default = new()
            {
                KickFret   = DefaultOrange,
                RedFret    = DefaultRed,
                YellowFret = DefaultYellow,
                BlueFret   = DefaultBlue,
                GreenFret  = DefaultGreen,

                KickFretInner   = DefaultOrange,
                RedFretInner    = DefaultRed,
                YellowFretInner = DefaultYellow,
                BlueFretInner   = DefaultBlue,
                GreenFretInner  = DefaultGreen,

                KickParticles   = Color.FromArgb(0xFF, 0xFF, 0xB6, 0x00),
                RedParticles    = DefaultRed,
                YellowParticles = DefaultYellow,
                BlueParticles   = DefaultBlue,
                GreenParticles  = DefaultGreen,

                KickNote = DefaultOrange,
                
                RedDrum    = DefaultRed,
                YellowDrum = DefaultYellow,
                BlueDrum   = DefaultBlue,
                GreenDrum  = DefaultGreen,
                
                RedCymbal    = DefaultRed,
                YellowCymbal = DefaultYellow,
                BlueCymbal   = DefaultBlue,
                GreenCymbal  = DefaultGreen,

                KickStarpower = DefaultStarpower,
                
                RedDrumStarpower    = DefaultStarpower,
                YellowDrumStarpower = DefaultStarpower,
                BlueDrumStarpower   = DefaultStarpower,
                GreenDrumStarpower  = DefaultStarpower,
                
                RedCymbalStarpower    = DefaultStarpower,
                YellowCymbalStarpower = DefaultStarpower,
                BlueCymbalStarpower   = DefaultStarpower,
                GreenCymbalStarpower  = DefaultStarpower,
            };


            #region Frets

            public Color KickFret;
            public Color RedFret;
            public Color YellowFret;
            public Color BlueFret;
            public Color GreenFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public readonly Color GetFretColor(int index)
            {
                return index switch
                {
                    0 => KickFret,
                    1 => RedFret,
                    2 => YellowFret,
                    3 => BlueFret,
                    4 => GreenFret,
                    _ => default
                };
            }

            public Color KickFretInner;
            public Color RedFretInner;
            public Color YellowFretInner;
            public Color BlueFretInner;
            public Color GreenFretInner;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public readonly Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    0 => KickFretInner,
                    1 => RedFretInner,
                    2 => YellowFretInner,
                    3 => BlueFretInner,
                    4 => GreenFretInner,
                    _ => default
                };
            }

            public Color KickParticles; // #FFB600
            public Color RedParticles;
            public Color YellowParticles;
            public Color BlueParticles;
            public Color GreenParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public readonly Color GetParticleColor(int index)
            {
                return index switch
                {
                    0 => KickParticles,
                    1 => RedParticles,
                    2 => YellowParticles,
                    3 => BlueParticles,
                    4 => GreenParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color KickNote;

            public Color RedDrum;
            public Color YellowDrum;
            public Color BlueDrum;
            public Color GreenDrum;

            public Color RedCymbal;
            public Color YellowCymbal;
            public Color BlueCymbal;
            public Color GreenCymbal;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public readonly Color GetNoteColor(int index)
            {
                return index switch
                {
                    0 => KickNote,

                    1 => RedDrum,
                    2 => YellowDrum,
                    3 => BlueDrum,
                    4 => GreenDrum,

                    5 => YellowCymbal,
                    6 => BlueCymbal,
                    7 => GreenCymbal,
                    8 => RedCymbal,

                    _ => default
                };
            }

            public Color KickStarpower;

            public Color RedDrumStarpower;
            public Color YellowDrumStarpower;
            public Color BlueDrumStarpower;
            public Color GreenDrumStarpower;

            public Color RedCymbalStarpower;
            public Color YellowCymbalStarpower;
            public Color BlueCymbalStarpower;
            public Color GreenCymbalStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public readonly Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    0 => KickStarpower,

                    1 => RedDrumStarpower,
                    2 => YellowDrumStarpower,
                    3 => BlueDrumStarpower,
                    4 => GreenDrumStarpower,

                    5 => YellowCymbalStarpower,
                    6 => BlueCymbalStarpower,
                    7 => GreenCymbalStarpower,
                    8 => RedCymbalStarpower,

                    _ => default
                };
            }

            public Color ActivationNote;

            #endregion

            #region Serialization

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(KickFret);
                writer.Write(RedFret);
                writer.Write(YellowFret);
                writer.Write(BlueFret);
                writer.Write(GreenFret);

                writer.Write(KickFretInner);
                writer.Write(RedFretInner);
                writer.Write(YellowFretInner);
                writer.Write(BlueFretInner);
                writer.Write(GreenFretInner);

                writer.Write(KickParticles);
                writer.Write(RedParticles);
                writer.Write(YellowParticles);
                writer.Write(BlueParticles);
                writer.Write(GreenParticles);

                writer.Write(KickNote);
                writer.Write(RedDrum);
                writer.Write(YellowDrum);
                writer.Write(BlueDrum);
                writer.Write(GreenDrum);

                writer.Write(RedCymbal);
                writer.Write(YellowCymbal);
                writer.Write(BlueCymbal);
                writer.Write(GreenCymbal);

                writer.Write(KickStarpower);
                writer.Write(RedDrumStarpower);
                writer.Write(YellowDrumStarpower);
                writer.Write(BlueDrumStarpower);
                writer.Write(GreenDrumStarpower);

                writer.Write(RedCymbalStarpower);
                writer.Write(YellowCymbalStarpower);
                writer.Write(BlueCymbalStarpower);
                writer.Write(GreenCymbalStarpower);

                writer.Write(ActivationNote);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                KickFret = reader.ReadColor();
                RedFret = reader.ReadColor();
                YellowFret = reader.ReadColor();
                BlueFret = reader.ReadColor();
                GreenFret = reader.ReadColor();

                KickFretInner = reader.ReadColor();
                RedFretInner = reader.ReadColor();
                YellowFretInner = reader.ReadColor();
                BlueFretInner = reader.ReadColor();
                GreenFretInner = reader.ReadColor();

                KickParticles = reader.ReadColor();
                RedParticles = reader.ReadColor();
                YellowParticles = reader.ReadColor();
                BlueParticles = reader.ReadColor();
                GreenParticles = reader.ReadColor();

                KickNote = reader.ReadColor();
                RedDrum = reader.ReadColor();
                YellowDrum = reader.ReadColor();
                BlueDrum = reader.ReadColor();
                GreenDrum = reader.ReadColor();

                RedCymbal = reader.ReadColor();
                YellowCymbal = reader.ReadColor();
                BlueCymbal = reader.ReadColor();
                GreenCymbal = reader.ReadColor();

                KickStarpower = reader.ReadColor();
                RedDrumStarpower = reader.ReadColor();
                YellowDrumStarpower = reader.ReadColor();
                BlueDrumStarpower = reader.ReadColor();
                GreenDrumStarpower = reader.ReadColor();

                RedCymbalStarpower = reader.ReadColor();
                YellowCymbalStarpower = reader.ReadColor();
                BlueCymbalStarpower = reader.ReadColor();
                GreenCymbalStarpower = reader.ReadColor();

                ActivationNote = reader.ReadColor();
            }

            #endregion
        }
    }
}