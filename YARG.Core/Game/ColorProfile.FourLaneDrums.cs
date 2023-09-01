using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FourLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color KickFret   = DefaultOrangeFret;
            public Color RedFret    = DefaultRedFret;
            public Color YellowFret = DefaultYellowFret;
            public Color BlueFret   = DefaultBlueFret;
            public Color GreenFret  = DefaultGreenFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretColor(int index)
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

            public Color KickFretInner   = DefaultOrangeFretInner;
            public Color RedFretInner    = DefaultRedFretInner;
            public Color YellowFretInner = DefaultYellowFretInner;
            public Color BlueFretInner   = DefaultBlueFretInner;
            public Color GreenFretInner  = DefaultGreenFretInner;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretInnerColor(int index)
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

            public Color KickParticles   = Color.FromArgb(0xFF, 0xFF, 0xB6, 0x00); // #ffb600
            public Color RedParticles    = DefaultRedParticles;
            public Color YellowParticles = DefaultYellowParticles;
            public Color BlueParticles   = DefaultBlueParticles;
            public Color GreenParticles  = DefaultGreenParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetParticleColor(int index)
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

            public Color KickNote = DefaultOrangeNote;

            public Color RedDrum    = DefaultRedNote;
            public Color YellowDrum = DefaultYellowNote;
            public Color BlueDrum   = DefaultBlueNote;
            public Color GreenDrum  = DefaultGreenNote;

            public Color YellowCymbal = DefaultYellowNote;
            public Color BlueCymbal   = DefaultBlueNote;
            public Color GreenCymbal  = DefaultGreenNote;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// </summary>
            public Color GetNoteColor(int index)
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

                    _ => default
                };
            }

            public Color KickStarpower = DefaultStarpower;

            public Color RedDrumStarpower    = DefaultStarpower;
            public Color YellowDrumStarpower = DefaultStarpower;
            public Color BlueDrumStarpower   = DefaultStarpower;
            public Color GreenDrumStarpower  = DefaultStarpower;

            public Color YellowCymbalStarpower = DefaultStarpower;
            public Color BlueCymbalStarpower   = DefaultStarpower;
            public Color GreenCymbalStarpower  = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
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

                    _ => default
                };
            }

            public Color ActivationNote = DefaultPurpleNote;

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

                writer.Write(YellowCymbal);
                writer.Write(BlueCymbal);
                writer.Write(GreenCymbal);

                writer.Write(KickStarpower);
                writer.Write(RedDrumStarpower);
                writer.Write(YellowDrumStarpower);
                writer.Write(BlueDrumStarpower);
                writer.Write(GreenDrumStarpower);

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

                YellowCymbal = reader.ReadColor();
                BlueCymbal = reader.ReadColor();
                GreenCymbal = reader.ReadColor();

                KickStarpower = reader.ReadColor();
                RedDrumStarpower = reader.ReadColor();
                YellowDrumStarpower = reader.ReadColor();
                BlueDrumStarpower = reader.ReadColor();
                GreenDrumStarpower = reader.ReadColor();

                YellowCymbalStarpower = reader.ReadColor();
                BlueCymbalStarpower = reader.ReadColor();
                GreenCymbalStarpower = reader.ReadColor();

                ActivationNote = reader.ReadColor();
            }

            #endregion
        }
    }
}