using System;
using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public struct FiveLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            public static readonly FiveLaneDrumsColors Default = new()
            {
                KickFret = Color.FromArgb(0xFF, 0xE6, 0x3F, 0xFF),
                RedFret = DefaultRed,
                YellowFret = DefaultYellow,
                BlueFret = DefaultBlue,
                OrangeFret = DefaultOrange,
                GreenFret = DefaultGreen,

                KickFretInner = DefaultOrange,
                RedFretInner = DefaultRed,
                YellowFretInner = DefaultYellow,
                BlueFretInner = DefaultBlue,
                OrangeFretInner = DefaultOrange,
                GreenFretInner = DefaultGreen,

                KickParticles = Color.FromArgb(0xFF, 0xD5, 0x00, 0xFF),
                RedParticles = DefaultRed,
                YellowParticles = DefaultYellow,
                BlueParticles = DefaultBlue,
                OrangeParticles = DefaultOrange,
                GreenParticles = DefaultGreen,

                KickNote = DefaultOrange,
                RedNote = DefaultRed,
                YellowNote = DefaultYellow,
                BlueNote = DefaultBlue,
                OrangeNote = DefaultOrange,
                GreenNote = DefaultGreen,

                KickStarpower = DefaultStarpower,
                RedStarpower = DefaultStarpower,
                YellowStarpower = DefaultStarpower,
                BlueStarpower = DefaultStarpower,
                OrangeStarpower = DefaultStarpower,
                GreenStarpower = DefaultStarpower,
            };


            #region Frets

            public Color KickFret; // #E63FFF;
            public Color RedFret;
            public Color YellowFret;
            public Color BlueFret;
            public Color OrangeFret;
            public Color GreenFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public readonly Color GetFretColor(int index)
            {
                return index switch
                {
                    0 => KickFret,
                    1 => RedFret,
                    2 => YellowFret,
                    3 => BlueFret,
                    4 => OrangeFret,
                    5 => GreenFret,
                    _ => default
                };
            }

            public Color KickFretInner;
            public Color RedFretInner;
            public Color YellowFretInner;
            public Color BlueFretInner;
            public Color OrangeFretInner;
            public Color GreenFretInner;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public readonly Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    0 => KickFretInner,
                    1 => RedFretInner,
                    2 => YellowFretInner,
                    3 => BlueFretInner,
                    4 => OrangeFretInner,
                    5 => GreenFretInner,
                    _ => default
                };
            }

            public Color KickParticles; // #D500FF
            public Color RedParticles;
            public Color YellowParticles;
            public Color BlueParticles;
            public Color OrangeParticles;
            public Color GreenParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public readonly Color GetParticleColor(int index)
            {
                return index switch
                {
                    0 => KickParticles,
                    1 => RedParticles,
                    2 => YellowParticles,
                    3 => BlueParticles,
                    4 => OrangeParticles,
                    5 => GreenParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color KickNote;

            public Color RedNote;
            public Color YellowNote;
            public Color BlueNote;
            public Color OrangeNote;
            public Color GreenNote;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public readonly Color GetNoteColor(int index)
            {
                return index switch
                {
                    0 => KickNote,

                    1 => RedNote,
                    2 => YellowNote,
                    3 => BlueNote,
                    4 => OrangeNote,
                    5 => GreenNote,

                    _ => default
                };
            }

            public Color KickStarpower;

            public Color RedStarpower;
            public Color YellowStarpower;
            public Color BlueStarpower;
            public Color OrangeStarpower;
            public Color GreenStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public readonly Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    0 => KickStarpower,

                    1 => RedStarpower,
                    2 => YellowStarpower,
                    3 => BlueStarpower,
                    4 => OrangeStarpower,
                    5 => GreenStarpower,

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

                writer.Write(ActivationNote);
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

                ActivationNote = reader.ReadColor();
            }

            #endregion
        }
    }
}