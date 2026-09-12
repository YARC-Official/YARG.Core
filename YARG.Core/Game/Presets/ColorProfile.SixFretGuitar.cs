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
            public Color BlackFretInner = DefaultGHLBlack;
            public Color WhiteFretInner = DefaultGHLWhite;

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

            public Color BlackParticles  = DefaultGHLBlack;
            public Color WhiteParticles  = DefaultGHLWhite;
            public Color OpenParticles   = DefaultGHLWhite;

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

            public Color BlackNote  = DefaultGHLBlack;
            public Color WhiteNote  = DefaultGHLWhite;
            public Color OpenNote  = DefaultGHLWhite;

            // Open HOPO/Tap notes have a dedicated color (mirrors 5-fret's OpenHopoNote).
            // The Open model's EmissionAddition may wash the color to white; the
            // dedicated field lets users control it independently of OpenNote.
            public Color OpenHopoNote          = DefaultGHLWhite;
            public Color OpenHopoNoteStarPower = DefaultGHLWhite;

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

            public Color BlackNoteStarPower = DefaultGHLBlack;
            public Color WhiteNoteStarPower = DefaultGHLWhite;
            public Color OpenNoteStarPower  = DefaultGHLWhite;

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

                writer.Write(OpenParticles);
                writer.Write(BlackParticles);
                writer.Write(WhiteParticles);

                writer.Write(OpenNote);
                writer.Write(BlackNote);
                writer.Write(WhiteNote);

                writer.Write(OpenNoteStarPower);
                writer.Write(BlackNoteStarPower);
                writer.Write(WhiteNoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                Fret = reader.ReadColor();

                BlackFretInner = reader.ReadColor();
                WhiteFretInner = reader.ReadColor();

                OpenParticles = reader.ReadColor();
                BlackParticles = reader.ReadColor();
                WhiteParticles = reader.ReadColor();

                OpenNote = reader.ReadColor();
                BlackNote = reader.ReadColor();
                WhiteNote = reader.ReadColor();

                OpenNoteStarPower = reader.ReadColor();
                BlackNoteStarPower = reader.ReadColor();
                WhiteNoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}
