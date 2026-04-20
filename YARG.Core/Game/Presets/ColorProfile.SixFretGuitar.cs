using System.Drawing;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class SixFretGuitarColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color Black1Fret  = Color.Black;
            public Color Black2Fret  = Color.Black;
            public Color Black3Fret  = Color.Black;
            public Color White1Fret  = Color.White;
            public Color White2Fret  = Color.White;
            public Color White3Fret  = Color.White;
            public Color OpenFret    = Color.White;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = Black 1, 5 = White 3.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => Black1Fret,
                    (int) SixFretGuitarFret.Black2 => Black2Fret,
                    (int) SixFretGuitarFret.Black3 => Black3Fret,
                    (int) SixFretGuitarFret.White1 => White1Fret,
                    (int) SixFretGuitarFret.White2 => White2Fret,
                    (int) SixFretGuitarFret.White3 => White3Fret,
                    (int) SixFretGuitarFret.Open => OpenFret,
                    _ => default
                };
            }

            public Color Black1FretInner  = Color.Black;
            public Color Black2FretInner  = Color.Black;
            public Color Black3FretInner  = Color.Black;
            public Color White1FretInner  = Color.White;
            public Color White2FretInner  = Color.White;
            public Color White3FretInner  = Color.White;
            public Color OpenFretInner    = Color.White;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = Black 1, 5 = White 3.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => Black1FretInner,
                    (int) SixFretGuitarFret.Black2 => Black2FretInner,
                    (int) SixFretGuitarFret.Black3 => Black3FretInner,
                    (int) SixFretGuitarFret.White1 => White1FretInner,
                    (int) SixFretGuitarFret.White2 => White2FretInner,
                    (int) SixFretGuitarFret.White3 => White3FretInner,
                    (int) SixFretGuitarFret.Open => OpenFretInner,
                    _ => default
                };
            }

            public Color Black1Particles  = Color.Black;
            public Color Black2Particles  = Color.Black;
            public Color Black3Particles  = Color.Black;
            public Color White1Particles  = Color.White;
            public Color White2Particles  = Color.White;
            public Color White3Particles  = Color.White;
            public Color OpenParticles    = Color.White;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = Black 1, 5 = White 3.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => Black1Particles,
                    (int) SixFretGuitarFret.Black2 => Black2Particles,
                    (int) SixFretGuitarFret.Black3 => Black3Particles,
                    (int) SixFretGuitarFret.White1 => White1Particles,
                    (int) SixFretGuitarFret.White2 => White2Particles,
                    (int) SixFretGuitarFret.White3 => White3Particles,
                    (int) SixFretGuitarFret.Open => OpenParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color Black1Note  = Color.Black;
            public Color Black2Note  = Color.Black;
            public Color Black3Note  = Color.Black;
            public Color White1Note  = Color.White;
            public Color White2Note  = Color.White;
            public Color White3Note  = Color.White;
            public Color OpenNote    = Color.White;
            public Color WildcardNote = DefaultWildcard;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = Black 1, 5 = White 3.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => Black1Note,
                    (int) SixFretGuitarFret.Black2 => Black2Note,
                    (int) SixFretGuitarFret.Black3 => Black3Note,
                    (int) SixFretGuitarFret.White1 => White1Note,
                    (int) SixFretGuitarFret.White2 => White2Note,
                    (int) SixFretGuitarFret.White3 => White3Note,
                    (int) SixFretGuitarFret.Wildcard => WildcardNote,
                    (int) SixFretGuitarFret.Open => OpenNote,
                    _ => default
                };
            }

            public Color Black1NoteStarPower  = DefaultStarpower;
            public Color Black2NoteStarPower  = DefaultStarpower;
            public Color Black3NoteStarPower  = DefaultStarpower;
            public Color White1NoteStarPower  = DefaultStarpower;
            public Color White2NoteStarPower  = DefaultStarpower;
            public Color White3NoteStarPower  = DefaultStarpower;
            public Color OpenNoteStarPower    = DefaultStarpower;
            public Color WildcardNoteStarPower = DefaultWildcardStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = Black 1, 5 = White 3.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => Black1NoteStarPower,
                    (int) SixFretGuitarFret.Black2 => Black2NoteStarPower,
                    (int) SixFretGuitarFret.Black3 => Black3NoteStarPower,
                    (int) SixFretGuitarFret.White1 => White1NoteStarPower,
                    (int) SixFretGuitarFret.White2 => White2NoteStarPower,
                    (int) SixFretGuitarFret.White3 => White3NoteStarPower,
                    (int) SixFretGuitarFret.Wildcard => WildcardNoteStarPower,
                    (int) SixFretGuitarFret.Open => OpenNoteStarPower,
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

            public SixFretGuitarColors Copy()
            {
                return (SixFretGuitarColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(Black1Fret);
                writer.Write(Black2Fret);
                writer.Write(Black3Fret);
                writer.Write(White1Fret);
                writer.Write(White2Fret);
                writer.Write(White3Fret);

                writer.Write(Black1FretInner);
                writer.Write(Black2FretInner);
                writer.Write(Black3FretInner);
                writer.Write(White1FretInner);
                writer.Write(White2FretInner);
                writer.Write(White3FretInner);

                writer.Write(Black1Particles);
                writer.Write(Black2Particles);
                writer.Write(Black3Particles);
                writer.Write(White1Particles);
                writer.Write(White2Particles);
                writer.Write(White3Particles);

                writer.Write(Black1Note);
                writer.Write(Black2Note);
                writer.Write(Black3Note);
                writer.Write(White1Note);
                writer.Write(White2Note);
                writer.Write(White3Note);

                writer.Write(Black1NoteStarPower);
                writer.Write(Black2NoteStarPower);
                writer.Write(Black3NoteStarPower);
                writer.Write(White1NoteStarPower);
                writer.Write(White2NoteStarPower);
                writer.Write(White3NoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                Black1Fret = reader.ReadColor();
                Black2Fret = reader.ReadColor();
                Black3Fret = reader.ReadColor();
                White1Fret = reader.ReadColor();
                White2Fret = reader.ReadColor();
                White3Fret = reader.ReadColor();

                Black1FretInner = reader.ReadColor();
                Black2FretInner = reader.ReadColor();
                Black3FretInner = reader.ReadColor();
                White1FretInner = reader.ReadColor();
                White2FretInner = reader.ReadColor();
                White3FretInner = reader.ReadColor();

                Black1Particles = reader.ReadColor();
                Black2Particles = reader.ReadColor();
                Black3Particles = reader.ReadColor();
                White1Particles = reader.ReadColor();
                White2Particles = reader.ReadColor();
                White3Particles = reader.ReadColor();

                Black1Note = reader.ReadColor();
                Black2Note = reader.ReadColor();
                Black3Note = reader.ReadColor();
                White1Note = reader.ReadColor();
                White2Note = reader.ReadColor();
                White3Note = reader.ReadColor();

                Black1NoteStarPower = reader.ReadColor();
                Black2NoteStarPower = reader.ReadColor();
                Black3NoteStarPower = reader.ReadColor();
                White1NoteStarPower = reader.ReadColor();
                White2NoteStarPower = reader.ReadColor();
                White3NoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}
