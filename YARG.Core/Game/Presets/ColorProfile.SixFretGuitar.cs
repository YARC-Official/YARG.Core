using System.Drawing;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class SixFretGuitarColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            // There is only one type of fret on six-fret, so all frets share a top color.
            // The inner color stays split black/white: it highlights which half of
            // the fret pad is currently pressed.
            public Color Fret           = Color.Gray;
            public Color BlackFretInner = Color.Black;
            public Color WhiteFretInner = Color.White;

            /// <summary>
            /// Gets the fret color for a specific fret index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
            /// All frets share a single color.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return Fret;
            }

            /// <summary>
            /// Gets the inner fret color for a specific fret index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
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
                    _ => default
                };
            }

            public Color BlackParticles  = Color.Black;
            public Color WhiteParticles  = Color.White;
            public Color OpenParticles   = Color.White;

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
                    (int) SixFretGuitarFret.Open => OpenParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color BlackNote  = Color.Black;
            public Color WhiteNote  = Color.White;
            public Color OpenNote   = Color.White;

            // Open HOPO/Tap notes have a dedicated color (mirrors 5-fret's OpenHopoNote).
            // The Open model's EmissionAddition may wash the color to white; the
            // dedicated field lets users control it independently of OpenNote.
            public Color OpenHopoNote          = Color.White;
            public Color OpenHopoNoteStarPower = Color.White;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
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
                    (int) SixFretGuitarFret.Wildcard => DefaultWildcard,
                    (int) SixFretGuitarFret.Open => OpenNote,
                    _ => default
                };
            }

            public Color BlackNoteStarPower = Color.Black;
            public Color WhiteNoteStarPower = Color.White;
            public Color OpenNoteStarPower  = Color.White;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// Black frets: 0=Black1, 1=Black2, 2=Black3.
            /// White frets: 3=White1, 4=White2, 5=White3.
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
                    (int) SixFretGuitarFret.Wildcard => DefaultWildcardStarpower,
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
                // Kinda yucky, but it's easier to maintain
                return (SixFretGuitarColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(Fret);
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
                Fret = reader.ReadColor();
                BlackFretInner = reader.ReadColor();
                WhiteFretInner = reader.ReadColor();

                BlackParticles = reader.ReadColor();
                WhiteParticles = reader.ReadColor();

                BlackNote = reader.ReadColor();
                WhiteNote = reader.ReadColor();

                BlackNoteStarPower = reader.ReadColor();
                WhiteNoteStarPower = reader.ReadColor();

                // Note: the Open* color fields are intentionally NOT binary-serialized
                // (replay format compatibility, no version bump). They persist through
                // the JSON preset files instead.
            }

            #endregion
        }
    }
}
