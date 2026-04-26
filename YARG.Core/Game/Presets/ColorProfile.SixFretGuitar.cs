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

            public Color BlackFret  = Color.Black;
            public Color WhiteFret  = Color.White;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// Open: 6.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => BlackFret,
                    (int) SixFretGuitarFret.Black2 => BlackFret,
                    (int) SixFretGuitarFret.Black3 => BlackFret,
                    (int) SixFretGuitarFret.White1 => WhiteFret,
                    (int) SixFretGuitarFret.White2 => WhiteFret,
                    (int) SixFretGuitarFret.White3 => WhiteFret,
                    (int) SixFretGuitarFret.Open => WhiteFret,
                    _ => default
                };
            }

            public Color BlackFretInner  = Color.Black;
            public Color WhiteFretInner  = Color.White;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// Open: 6.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => BlackFretInner,
                    (int) SixFretGuitarFret.Black2 => BlackFretInner,
                    (int) SixFretGuitarFret.Black3 => BlackFretInner,
                    (int) SixFretGuitarFret.White1 => WhiteFretInner,
                    (int) SixFretGuitarFret.White2 => WhiteFretInner,
                    (int) SixFretGuitarFret.White3 => WhiteFretInner,
                    (int) SixFretGuitarFret.Open => WhiteFretInner,
                    _ => default
                };
            }

            public Color BlackParticles  = Color.Black;
            public Color WhiteParticles  = Color.White;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// Open: 6.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => BlackParticles,
                    (int) SixFretGuitarFret.Black2 => BlackParticles,
                    (int) SixFretGuitarFret.Black3 => BlackParticles,
                    (int) SixFretGuitarFret.White1 => WhiteParticles,
                    (int) SixFretGuitarFret.White2 => WhiteParticles,
                    (int) SixFretGuitarFret.White3 => WhiteParticles,
                    (int) SixFretGuitarFret.Open => WhiteParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color BlackNote  = Color.Black;
            public Color WhiteNote  = Color.White;
            public Color WildcardNote = DefaultWildcard;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// Wildcard and Open: 6, 7.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => BlackNote,
                    (int) SixFretGuitarFret.Black2 => BlackNote,
                    (int) SixFretGuitarFret.Black3 => BlackNote,
                    (int) SixFretGuitarFret.White1 => WhiteNote,
                    (int) SixFretGuitarFret.White2 => WhiteNote,
                    (int) SixFretGuitarFret.White3 => WhiteNote,
                    (int) SixFretGuitarFret.Wildcard => WildcardNote,
                    (int) SixFretGuitarFret.Open => WhiteNote,
                    _ => default
                };
            }

            public Color BlackNoteStarPower = Color.Black;
            public Color WhiteNoteStarPower = Color.White;
            public Color WildcardNoteStarPower = DefaultWildcardStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// Wildcard and Open: 6, 7.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    (int) SixFretGuitarFret.Black1 => BlackNoteStarPower,
                    (int) SixFretGuitarFret.Black2 => BlackNoteStarPower,
                    (int) SixFretGuitarFret.Black3 => BlackNoteStarPower,
                    (int) SixFretGuitarFret.White1 => WhiteNoteStarPower,
                    (int) SixFretGuitarFret.White2 => WhiteNoteStarPower,
                    (int) SixFretGuitarFret.White3 => WhiteNoteStarPower,
                    (int) SixFretGuitarFret.Wildcard => WildcardNoteStarPower,
                    (int) SixFretGuitarFret.Open => WhiteNoteStarPower,
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
                writer.Write(BlackFret);
                writer.Write(WhiteFret);

                writer.Write(BlackFretInner);
                writer.Write(WhiteFretInner);

                writer.Write(BlackParticles);
                writer.Write(WhiteParticles);

                writer.Write(BlackNote);
                writer.Write(WhiteNote);

                writer.Write(BlackNoteStarPower);
                writer.Write(WhiteNoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                BlackFret = reader.ReadColor();
                WhiteFret = reader.ReadColor();

                BlackFretInner = reader.ReadColor();
                WhiteFretInner = reader.ReadColor();

                BlackParticles = reader.ReadColor();
                WhiteParticles = reader.ReadColor();

                BlackNote = reader.ReadColor();
                WhiteNote = reader.ReadColor();

                BlackNoteStarPower = reader.ReadColor();
                WhiteNoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}
