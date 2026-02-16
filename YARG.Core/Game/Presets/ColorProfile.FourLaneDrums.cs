using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public enum FourLaneDrumsFret
        {
            Kick,

            RedDrum,
            YellowDrum,
            BlueDrum,
            GreenDrum,

            RedCymbal,
            YellowCymbal,
            BlueCymbal,
            GreenCymbal
        }

        public class FourLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color KickFret   = DefaultOrange;
            public Color RedFret    = DefaultRed;
            public Color YellowFret = DefaultYellow;
            public Color BlueFret   = DefaultBlue;
            public Color GreenFret  = DefaultGreen;

            // Exclusive to split view
            public Color RedCymbalFret = DefaultRed;
            public Color YellowCymbalFret = DefaultYellow;
            public Color BlueCymbalFret = DefaultBlue;
            public Color GreenCymbalFret = DefaultGreen;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickFret,
                    (int)FourLaneDrumsFret.RedDrum => RedFret,
                    (int)FourLaneDrumsFret.YellowDrum => YellowFret,
                    (int)FourLaneDrumsFret.BlueDrum => BlueFret,
                    (int)FourLaneDrumsFret.GreenDrum => GreenFret,

                    // Exclusive to split view
                    (int)FourLaneDrumsFret.RedCymbal => RedCymbalFret,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbalFret,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbalFret,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbalFret,

                    _ => default
                };
            }

            public Color KickFretInner   = DefaultOrange;
            public Color RedFretInner    = DefaultRed;
            public Color YellowFretInner = DefaultYellow;
            public Color BlueFretInner   = DefaultBlue;
            public Color GreenFretInner  = DefaultGreen;

            // Exclusive to split view
            public Color RedCymbalFretInner = DefaultRed;
            public Color YellowCymbalFretInner = DefaultYellow;
            public Color BlueCymbalFretInner = DefaultBlue;
            public Color GreenCymbalFretInner = DefaultGreen;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickFretInner,
                    (int)FourLaneDrumsFret.RedDrum => RedFretInner,
                    (int)FourLaneDrumsFret.YellowDrum => YellowFretInner,
                    (int)FourLaneDrumsFret.BlueDrum => BlueFretInner,
                    (int)FourLaneDrumsFret.GreenDrum => GreenFretInner,

                    // Exclusive to split view
                    (int)FourLaneDrumsFret.RedCymbal => RedCymbalFretInner,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbalFretInner,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbalFretInner,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbalFretInner,

                    _ => default
                };
            }

            public Color KickParticles   = Color.FromArgb(0xFF, 0xFF, 0xB6, 0x00); // #FFB600
            public Color RedParticles    = DefaultRed;
            public Color YellowParticles = DefaultYellow;
            public Color BlueParticles   = DefaultBlue;
            public Color GreenParticles  = DefaultGreen;

            // Exclusive to split view
            public Color RedCymbalParticles = DefaultRed;
            public Color YellowCymbalParticles = DefaultYellow;
            public Color BlueCymbalParticles = DefaultBlue;
            public Color GreenCymbalParticles = DefaultGreen;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickParticles,
                    (int)FourLaneDrumsFret.RedDrum => RedParticles,
                    (int)FourLaneDrumsFret.YellowDrum => YellowParticles,
                    (int)FourLaneDrumsFret.BlueDrum => BlueParticles,
                    (int)FourLaneDrumsFret.GreenDrum => GreenParticles,

                    // Exclusive to split view
                    (int)FourLaneDrumsFret.RedCymbal => RedCymbalParticles,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbalParticles,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbalParticles,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbalParticles,

                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color KickNote = DefaultOrange;

            public Color RedDrum    = DefaultRed;
            public Color YellowDrum = DefaultYellow;
            public Color BlueDrum   = DefaultBlue;
            public Color GreenDrum  = DefaultGreen;

            public Color RedCymbal    = DefaultRedCymbal;
            public Color YellowCymbal = DefaultYellowCymbal;
            public Color BlueCymbal   = DefaultBlueCymbal;
            public Color GreenCymbal  = DefaultGreenCymbal;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickNote,

                    (int)FourLaneDrumsFret.RedDrum => RedDrum,
                    (int)FourLaneDrumsFret.YellowDrum => YellowDrum,
                    (int)FourLaneDrumsFret.BlueDrum => BlueDrum,
                    (int) FourLaneDrumsFret.GreenDrum => GreenDrum,

                    (int)FourLaneDrumsFret.RedCymbal => RedCymbal,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbal,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbal,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbal,

                    _ => default
                };
            }

            public Color KickStarpower = DefaultStarpower;

            public Color RedDrumStarpower    = DefaultStarpower;
            public Color YellowDrumStarpower = DefaultStarpower;
            public Color BlueDrumStarpower   = DefaultStarpower;
            public Color GreenDrumStarpower  = DefaultStarpower;

            public Color RedCymbalStarpower    = DefaultStarpower;
            public Color YellowCymbalStarpower = DefaultStarpower;
            public Color BlueCymbalStarpower   = DefaultStarpower;
            public Color GreenCymbalStarpower  = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickStarpower,

                    (int)FourLaneDrumsFret.RedDrum => RedDrumStarpower,
                    (int)FourLaneDrumsFret.YellowDrum => YellowDrumStarpower,
                    (int)FourLaneDrumsFret.BlueDrum => BlueDrumStarpower,
                    (int)FourLaneDrumsFret.GreenDrum => GreenDrumStarpower,

                    (int)FourLaneDrumsFret.RedCymbal => RedCymbalStarpower,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbalStarpower,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbalStarpower,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbalStarpower,

                    _ => default
                };
            }

            public Color KickActivationNote = DefaultOrangeActivationNote;

            public Color RedPadActivationNote    = DefaultRedActivationNote;
            public Color YellowPadActivationNote = DefaultYellowActivationNote;
            public Color BluePadActivationNote   = DefaultBlueActivationNote;
            public Color GreenPadActivationNote  = DefaultGreenActivationNote;

            public Color RedCymbalActivationNote    = DefaultRedActivationNote;
            public Color YellowCymbalActivationNote = DefaultYellowActivationNote;
            public Color BlueCymbalActivationNote   = DefaultBlueActivationNote;
            public Color GreenCymbalActivationNote  = DefaultGreenActivationNote;

            /// <summary>
            /// Gets the activation note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public Color GetActivationNoteColor(int index)
            {
                return index switch
                {
                    (int)FourLaneDrumsFret.Kick => KickActivationNote,

                    (int)FourLaneDrumsFret.RedDrum => RedPadActivationNote,
                    (int)FourLaneDrumsFret.YellowDrum => YellowPadActivationNote,
                    (int)FourLaneDrumsFret.BlueDrum => BluePadActivationNote,
                    (int)FourLaneDrumsFret.GreenDrum => GreenPadActivationNote,

                    (int)FourLaneDrumsFret.RedCymbal => RedCymbalActivationNote,
                    (int)FourLaneDrumsFret.YellowCymbal => YellowCymbalActivationNote,
                    (int)FourLaneDrumsFret.BlueCymbal => BlueCymbalActivationNote,
                    (int)FourLaneDrumsFret.GreenCymbal => GreenCymbalActivationNote,

                    _ => default
                };
            }

            #region Metal

            public Color Metal          = DefaultMetal;
            public Color MetalStarPower = DefaultMetalStarPower;

            public Color GetMetalColor(bool isForStarPower)
            {
                return isForStarPower ? MetalStarPower : Metal;
            }

            #endregion

            #region Miss Effect

            public Color Miss = DefaultMiss;

            #endregion

            #endregion

            #region Serialization

            public FourLaneDrumsColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (FourLaneDrumsColors) MemberwiseClone();
            }

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

                writer.Write(KickActivationNote);

                writer.Write(RedPadActivationNote);
                writer.Write(YellowPadActivationNote);
                writer.Write(BluePadActivationNote);
                writer.Write(GreenPadActivationNote);

                writer.Write(RedCymbalActivationNote);
                writer.Write(YellowCymbalActivationNote);
                writer.Write(BlueCymbalActivationNote);
                writer.Write(GreenCymbalActivationNote);

                writer.Write(Metal);
                writer.Write(MetalStarPower);

                writer.Write(RedCymbalFret);
                writer.Write(YellowCymbalFret);
                writer.Write(BlueCymbalFret);
                writer.Write(GreenCymbalFret);

                writer.Write(RedCymbalFretInner);
                writer.Write(YellowCymbalFretInner);
                writer.Write(BlueCymbalFretInner);
                writer.Write(GreenCymbalFretInner);

                writer.Write(RedCymbalParticles);
                writer.Write(YellowCymbalParticles);
                writer.Write(BlueCymbalParticles);
                writer.Write(GreenCymbalParticles);
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

                KickActivationNote = reader.ReadColor();

                RedPadActivationNote = reader.ReadColor();
                YellowPadActivationNote = reader.ReadColor();
                BluePadActivationNote = reader.ReadColor();
                GreenPadActivationNote = reader.ReadColor();

                RedCymbalActivationNote = reader.ReadColor();
                YellowCymbalActivationNote = reader.ReadColor();
                BlueCymbalActivationNote = reader.ReadColor();
                GreenCymbalActivationNote = reader.ReadColor();

                Metal = reader.ReadColor();
                MetalStarPower = reader.ReadColor();

                RedCymbalFret = reader.ReadColor();
                YellowCymbalFret = reader.ReadColor();
                BlueCymbalFret = reader.ReadColor();
                GreenCymbalFret = reader.ReadColor();

                RedCymbalFretInner = reader.ReadColor();
                YellowCymbalFretInner = reader.ReadColor();
                BlueCymbalFretInner = reader.ReadColor();
                GreenCymbalFretInner = reader.ReadColor();

                RedCymbalParticles = reader.ReadColor();
                YellowCymbalParticles = reader.ReadColor();
                BlueCymbalParticles = reader.ReadColor();
                GreenCymbalParticles = reader.ReadColor();

            }

            #endregion
        }
    }
}