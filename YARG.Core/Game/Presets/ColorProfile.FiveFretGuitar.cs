using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public struct FiveFretGuitarColors : IFretColorProvider, IBinarySerializable
        {
            public static readonly FiveFretGuitarColors Default = new()
            {
                OpenFret = DefaultPurple,
                GreenFret = DefaultGreen,
                RedFret = DefaultRed,
                YellowFret = DefaultYellow,
                BlueFret = DefaultBlue,
                OrangeFret = DefaultOrange,

                OpenFretInner = DefaultPurple,
                GreenFretInner = DefaultGreen,
                RedFretInner = DefaultRed,
                YellowFretInner = DefaultYellow,
                BlueFretInner = DefaultBlue,
                OrangeFretInner = DefaultOrange,

                OpenParticles = DefaultPurple,
                GreenParticles = DefaultGreen,
                RedParticles = DefaultRed,
                YellowParticles = DefaultYellow,
                BlueParticles = DefaultBlue,
                OrangeParticles = DefaultOrange,

                OpenNote = DefaultPurple,
                GreenNote = DefaultGreen,
                RedNote = DefaultRed,
                YellowNote = DefaultYellow,
                BlueNote = DefaultBlue,
                OrangeNote = DefaultOrange,

                OpenNoteStarPower = DefaultStarpower,
                GreenNoteStarPower = DefaultStarpower,
                RedNoteStarPower = DefaultStarpower,
                YellowNoteStarPower = DefaultStarpower,
                BlueNoteStarPower = DefaultStarpower,
                OrangeNoteStarPower = DefaultStarpower,
            };

            #region Frets

            public Color OpenFret;
            public Color GreenFret;
            public Color RedFret;
            public Color YellowFret;
            public Color BlueFret;
            public Color OrangeFret;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public readonly Color GetFretColor(int index)
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

            public Color OpenFretInner;
            public Color GreenFretInner;
            public Color RedFretInner;
            public Color YellowFretInner;
            public Color BlueFretInner;
            public Color OrangeFretInner;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public readonly Color GetFretInnerColor(int index)
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

            public Color OpenParticles;
            public Color GreenParticles;
            public Color RedParticles;
            public Color YellowParticles;
            public Color BlueParticles;
            public Color OrangeParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public readonly Color GetParticleColor(int index)
            {
                return index switch
                {
                    0 => OpenParticles,
                    1 => GreenParticles,
                    2 => RedParticles,
                    3 => YellowParticles,
                    4 => BlueParticles,
                    5 => OrangeParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color OpenNote;
            public Color GreenNote;
            public Color RedNote;
            public Color YellowNote;
            public Color BlueNote;
            public Color OrangeNote;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public readonly Color GetNoteColor(int index)
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

            public Color OpenNoteStarPower;
            public Color GreenNoteStarPower;
            public Color RedNoteStarPower;
            public Color YellowNoteStarPower;
            public Color BlueNoteStarPower;
            public Color OrangeNoteStarPower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public readonly Color GetNoteStarPowerColor(int index)
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

            #endregion

            #region Serialization

            public readonly void Serialize(BinaryWriter writer)
            {
                writer.Write(OpenFret);
                writer.Write(GreenFret);
                writer.Write(RedFret);
                writer.Write(YellowFret);
                writer.Write(BlueFret);
                writer.Write(OrangeFret);

                writer.Write(OpenFretInner);
                writer.Write(GreenFretInner);
                writer.Write(RedFretInner);
                writer.Write(YellowFretInner);
                writer.Write(BlueFretInner);
                writer.Write(OrangeFretInner);

                writer.Write(OpenParticles);
                writer.Write(GreenParticles);
                writer.Write(RedParticles);
                writer.Write(YellowParticles);
                writer.Write(BlueParticles);
                writer.Write(OrangeParticles);

                writer.Write(OpenNote);
                writer.Write(GreenNote);
                writer.Write(RedNote);
                writer.Write(YellowNote);
                writer.Write(BlueNote);
                writer.Write(OrangeNote);

                writer.Write(OpenNoteStarPower);
                writer.Write(GreenNoteStarPower);
                writer.Write(RedNoteStarPower);
                writer.Write(YellowNoteStarPower);
                writer.Write(BlueNoteStarPower);
                writer.Write(OrangeNoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                OpenFret = reader.ReadColor();
                GreenFret = reader.ReadColor();
                RedFret = reader.ReadColor();
                YellowFret = reader.ReadColor();
                BlueFret = reader.ReadColor();
                OrangeFret = reader.ReadColor();

                OpenFretInner = reader.ReadColor();
                GreenFretInner = reader.ReadColor();
                RedFretInner = reader.ReadColor();
                YellowFretInner = reader.ReadColor();
                BlueFretInner = reader.ReadColor();
                OrangeFretInner = reader.ReadColor();

                OpenParticles = reader.ReadColor();
                GreenParticles = reader.ReadColor();
                RedParticles = reader.ReadColor();
                YellowParticles = reader.ReadColor();
                BlueParticles = reader.ReadColor();
                OrangeParticles = reader.ReadColor();

                OpenNote = reader.ReadColor();
                GreenNote = reader.ReadColor();
                RedNote = reader.ReadColor();
                YellowNote = reader.ReadColor();
                BlueNote = reader.ReadColor();
                OrangeNote = reader.ReadColor();

                OpenNoteStarPower = reader.ReadColor();
                GreenNoteStarPower = reader.ReadColor();
                RedNoteStarPower = reader.ReadColor();
                YellowNoteStarPower = reader.ReadColor();
                BlueNoteStarPower = reader.ReadColor();
                OrangeNoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}