﻿using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FiveLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color KickFret   = DefaultPurpleFret;
            public Color RedFret    = DefaultRedFret;
            public Color YellowFret = DefaultYellowFret;
            public Color BlueFret   = DefaultBlueFret;
            public Color OrangeFret = DefaultOrangeFret;
            public Color GreenFret  = DefaultGreenFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetFretColor(int index)
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

            public Color KickFretInner   = DefaultPurpleFret;
            public Color RedFretInner    = DefaultRedFretInner;
            public Color YellowFretInner = DefaultYellowFretInner;
            public Color BlueFretInner   = DefaultBlueFretInner;
            public Color OrangeFretInner = DefaultOrangeFretInner;
            public Color GreenFretInner  = DefaultGreenFretInner;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetFretInnerColor(int index)
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

            public Color KickParticles   = Color.FromArgb(0xFF, 0xD5, 0x00, 0xFF); // #d500ff
            public Color RedParticles    = DefaultRedParticles;
            public Color YellowParticles = DefaultYellowParticles;
            public Color BlueParticles   = DefaultBlueParticles;
            public Color OrangeParticles = DefaultOrangeParticles;
            public Color GreenParticles  = DefaultGreenParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 5 = green.
            /// </summary>
            public Color GetParticleColor(int index)
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

            public Color KickNote = DefaultPurpleNote;

            public Color RedNote    = DefaultRedNote;
            public Color YellowNote = DefaultYellowNote;
            public Color BlueNote   = DefaultBlueNote;
            public Color OrangeNote = DefaultOrangeNote;
            public Color GreenNote  = DefaultGreenNote;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public Color GetNoteColor(int index)
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

            public Color KickStarpower = DefaultStarpower;

            public Color RedStarpower    = DefaultStarpower;
            public Color YellowStarpower = DefaultStarpower;
            public Color BlueStarpower   = DefaultStarpower;
            public Color OrangeStarpower = DefaultStarpower;
            public Color GreenStarpower  = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 5 = green drum.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
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

            public Color ActivationNote = DefaultPurpleNote;

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