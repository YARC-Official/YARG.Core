using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FiveFretGuitarColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color OpenFret   = DefaultPurpleFret;
            public Color GreenFret  = DefaultGreenFret;
            public Color RedFret    = DefaultRedFret;
            public Color YellowFret = DefaultYellowFret;
            public Color BlueFret   = DefaultBlueFret;
            public Color OrangeFret = DefaultOrangeFret;

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

            public Color OpenFretInner   = DefaultPurpleFretInner;
            public Color GreenFretInner  = DefaultGreenFretInner;
            public Color RedFretInner    = DefaultRedFretInner;
            public Color YellowFretInner = DefaultYellowFretInner;
            public Color BlueFretInner   = DefaultBlueFretInner;
            public Color OrangeFretInner = DefaultOrangeFretInner;

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

            public Color OpenParticles   = DefaultPurpleParticles;
            public Color GreenParticles  = DefaultGreenParticles;
            public Color RedParticles    = DefaultRedParticles;
            public Color YellowParticles = DefaultYellowParticles;
            public Color BlueParticles   = DefaultBlueParticles;
            public Color OrangeParticles = DefaultOrangeParticles;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetParticleColor(int index)
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

            public Color OpenNote   = DefaultPurpleNote;
            public Color GreenNote  = DefaultGreenNote;
            public Color RedNote    = DefaultRedNote;
            public Color YellowNote = DefaultYellowNote;
            public Color BlueNote   = DefaultBlueNote;
            public Color OrangeNote = DefaultOrangeNote;

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

            public Color OpenNoteStarPower   = DefaultStarpower;
            public Color GreenNoteStarPower  = DefaultStarpower;
            public Color RedNoteStarPower    = DefaultStarpower;
            public Color YellowNoteStarPower = DefaultStarpower;
            public Color BlueNoteStarPower   = DefaultStarpower;
            public Color OrangeNoteStarPower = DefaultStarpower;

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

            #endregion

            #region Serialization

            public FiveFretGuitarColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (FiveFretGuitarColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
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