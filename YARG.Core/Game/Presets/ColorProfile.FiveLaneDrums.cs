using DG.Tweening;
using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public enum FiveLaneDrumsFret
        {
            Kick,
            Red,
            Yellow,
            Blue,
            Orange,
            Green,

            // Exclusive to split-dedicated kick lanes
            DoubleKick
        }

        public class FiveLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color KickFret   = Color.FromArgb(0xFF, 0xE6, 0x3F, 0xFF); // #E63FFF;
            public Color RedFret    = DefaultRed;
            public Color YellowFret = DefaultYellow;
            public Color BlueFret   = DefaultBlue;
            public Color OrangeFret = DefaultOrange;
            public Color GreenFret  = DefaultGreen;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickFret = DefaultSilverFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    (int)FiveLaneDrumsFret.Kick => KickFret,
                    (int)FiveLaneDrumsFret.Red => RedFret,
                    (int)FiveLaneDrumsFret.Yellow => YellowFret,
                    (int)FiveLaneDrumsFret.Blue => BlueFret,
                    (int)FiveLaneDrumsFret.Orange => OrangeFret,
                    (int)FiveLaneDrumsFret.Green => GreenFret,

                    // Exclusive to split-dedicated kick lanes
                    (int)FiveLaneDrumsFret.DoubleKick => DoubleKickFret,
                    _ => default
                };
            }

            public Color KickFretInner   = DefaultPurple;
            public Color RedFretInner    = DefaultRed;
            public Color YellowFretInner = DefaultYellow;
            public Color BlueFretInner   = DefaultBlue;
            public Color OrangeFretInner = DefaultOrange;
            public Color GreenFretInner  = DefaultGreen;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickFretInner = DefaultSilverFret;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    (int)FiveLaneDrumsFret.Kick => KickFretInner,
                    (int)FiveLaneDrumsFret.Red => RedFretInner,
                    (int)FiveLaneDrumsFret.Yellow => YellowFretInner,
                    (int)FiveLaneDrumsFret.Blue => BlueFretInner,
                    (int)FiveLaneDrumsFret.Orange => OrangeFretInner,
                    (int) FiveLaneDrumsFret.Green => GreenFretInner,

                    // Exclusive to split-dedicated kick lanes
                    (int)FiveLaneDrumsFret.DoubleKick => DoubleKickFretInner,
                    _ => default
                };
            }

            public Color KickParticles   = Color.FromArgb(0xFF, 0xD5, 0x00, 0xFF); // #D500FF
            public Color RedParticles    = DefaultRed;
            public Color YellowParticles = DefaultYellow;
            public Color BlueParticles   = DefaultBlue;
            public Color OrangeParticles = DefaultOrange;
            public Color GreenParticles  = DefaultGreen;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickParticles = DefaultSilverFret;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    (int)FiveLaneDrumsFret.Kick => KickParticles,
                    (int)FiveLaneDrumsFret.Red => RedParticles,
                    (int)FiveLaneDrumsFret.Yellow => YellowParticles,
                    (int)FiveLaneDrumsFret.Blue => BlueParticles,
                    (int)FiveLaneDrumsFret.Orange => OrangeParticles,
                    (int) FiveLaneDrumsFret.Green => GreenParticles,

                    // Exclusive to split-dedicated kick lanes
                    (int)FiveLaneDrumsFret.DoubleKick => DoubleKickParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color KickNote = DefaultPurple;

            public Color RedNote    = DefaultRed;
            public Color YellowNote = DefaultYellow;
            public Color BlueNote   = DefaultBlue;
            public Color OrangeNote = DefaultOrange;
            public Color GreenNote  = DefaultGreen;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickNote = DefaultSilver;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    (int)FiveLaneDrumsFret.Kick => KickNote,

                    (int)FiveLaneDrumsFret.Red => RedNote,
                    (int)FiveLaneDrumsFret.Yellow => YellowNote,
                    (int)FiveLaneDrumsFret.Blue => BlueNote,
                    (int)FiveLaneDrumsFret.Orange => OrangeNote,
                    (int)FiveLaneDrumsFret.Green => GreenNote,

                    // Exclusive to split-dedicated kick lanes
                    (int)FiveLaneDrumsFret.DoubleKick => DoubleKickNote,

                    _ => default
                };
            }

            public Color KickStarpower = DefaultStarpower;

            public Color RedStarpower    = DefaultStarpower;
            public Color YellowStarpower = DefaultStarpower;
            public Color BlueStarpower   = DefaultStarpower;
            public Color OrangeStarpower = DefaultStarpower;
            public Color GreenStarpower  = DefaultStarpower;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickStarpower = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    (int)FiveLaneDrumsFret.Kick => KickStarpower,

                    (int)FiveLaneDrumsFret.Red => RedStarpower,
                    (int)FiveLaneDrumsFret.Yellow => YellowStarpower,
                    (int)FiveLaneDrumsFret.Blue => BlueStarpower,
                    (int)FiveLaneDrumsFret.Orange => OrangeStarpower,
                    (int) FiveLaneDrumsFret.Green => GreenStarpower,

                    // Exclusive to split-dedicated kick lanes
                    (int) FiveLaneDrumsFret.DoubleKick => DoubleKickStarpower,

                    _ => default
                };
            }

            public Color KickActivationNote = DefaultPurpleActivationNote;

            public Color RedActivationNote    = DefaultRedActivationNote;
            public Color YellowActivationNote = DefaultYellowActivationNote;
            public Color BlueActivationNote   = DefaultBlueActivationNote;
            public Color OrangeActivationNote = DefaultOrangeActivationNote;
            public Color GreenActivationNote  = DefaultGreenActivationNote;

            // Exclusive to split-dedicated kick lanes
            public Color DoubleKickActivationNote = DefaultSilverActivationNote;

            /// <summary>
            /// Gets the activation note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public Color GetActivationNoteColor(int index)
            {
                return index switch
                {
                    (int) FiveLaneDrumsFret.Kick => KickActivationNote,

                    (int)FiveLaneDrumsFret.Red => RedActivationNote,
                    (int)FiveLaneDrumsFret.Yellow => YellowActivationNote,
                    (int)FiveLaneDrumsFret.Blue => BlueActivationNote,
                    (int)FiveLaneDrumsFret.Orange => OrangeActivationNote,
                    (int) FiveLaneDrumsFret.Green => GreenActivationNote,

                    (int) FiveLaneDrumsFret.DoubleKick => DoubleKickActivationNote,

                    _ => default
                };
            }

            #endregion

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

            #region Serialization

            public FiveLaneDrumsColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (FiveLaneDrumsColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(KickFret);
                writer.Write(RedFret);
                writer.Write(YellowFret);
                writer.Write(BlueFret);
                writer.Write(OrangeFret);
                writer.Write(GreenFret);

                writer.Write(KickFretInner);
                writer.Write(RedFretInner);
                writer.Write(YellowFretInner);
                writer.Write(BlueFretInner);
                writer.Write(OrangeFretInner);
                writer.Write(GreenFretInner);

                writer.Write(KickParticles);
                writer.Write(RedParticles);
                writer.Write(YellowParticles);
                writer.Write(BlueParticles);
                writer.Write(OrangeParticles);
                writer.Write(GreenParticles);

                writer.Write(KickNote);
                writer.Write(RedNote);
                writer.Write(YellowNote);
                writer.Write(BlueNote);
                writer.Write(OrangeNote);
                writer.Write(GreenNote);

                writer.Write(KickStarpower);
                writer.Write(RedStarpower);
                writer.Write(YellowStarpower);
                writer.Write(BlueStarpower);
                writer.Write(OrangeStarpower);
                writer.Write(GreenStarpower);

                writer.Write(KickActivationNote);

                writer.Write(RedActivationNote);
                writer.Write(YellowActivationNote);
                writer.Write(BlueActivationNote);
                writer.Write(OrangeActivationNote);
                writer.Write(GreenActivationNote);

                writer.Write(Metal);
                writer.Write(MetalStarPower);

                writer.Write(DoubleKickFret);
                writer.Write(DoubleKickFretInner);
                writer.Write(DoubleKickParticles);
                writer.Write(DoubleKickNote);
                writer.Write(DoubleKickStarpower);
                writer.Write(DoubleKickActivationNote);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                KickFret = reader.ReadColor();
                RedFret = reader.ReadColor();
                YellowFret = reader.ReadColor();
                BlueFret = reader.ReadColor();
                OrangeFret = reader.ReadColor();
                GreenFret = reader.ReadColor();

                KickFretInner = reader.ReadColor();
                RedFretInner = reader.ReadColor();
                YellowFretInner = reader.ReadColor();
                BlueFretInner = reader.ReadColor();
                OrangeFretInner = reader.ReadColor();
                GreenFretInner = reader.ReadColor();

                KickParticles = reader.ReadColor();
                RedParticles = reader.ReadColor();
                YellowParticles = reader.ReadColor();
                BlueParticles = reader.ReadColor();
                OrangeParticles = reader.ReadColor();
                GreenParticles = reader.ReadColor();

                KickNote = reader.ReadColor();
                RedNote = reader.ReadColor();
                YellowNote = reader.ReadColor();
                BlueNote = reader.ReadColor();
                OrangeNote = reader.ReadColor();
                GreenNote = reader.ReadColor();

                KickStarpower = reader.ReadColor();
                RedStarpower = reader.ReadColor();
                YellowStarpower = reader.ReadColor();
                BlueStarpower = reader.ReadColor();
                OrangeStarpower = reader.ReadColor();
                GreenStarpower = reader.ReadColor();

                KickActivationNote = reader.ReadColor();

                RedActivationNote = reader.ReadColor();
                YellowActivationNote = reader.ReadColor();
                BlueActivationNote = reader.ReadColor();
                OrangeActivationNote = reader.ReadColor();
                GreenActivationNote = reader.ReadColor();

                Metal = reader.ReadColor();
                MetalStarPower = reader.ReadColor();

                DoubleKickFret = reader.ReadColor();
                DoubleKickFretInner = reader.ReadColor();
                DoubleKickParticles = reader.ReadColor();
                DoubleKickNote = reader.ReadColor();
                DoubleKickStarpower = reader.ReadColor();
                DoubleKickActivationNote = reader.ReadColor();
            }

            #endregion
        }
    }
}