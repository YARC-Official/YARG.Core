using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class VocalsColors : IBinarySerializable
        {
            // These defaults match the colors that were previously hard-coded in VocalTrack.
            public Color LeadVocals = Color.FromArgb(0xFF, 0x00, 0xCC, 0xFF); // #00CCFF
            public Color Harmony1   = Color.FromArgb(0xFF, 0x00, 0xCC, 0xFF); // #00CCFF
            public Color Harmony2   = Color.FromArgb(0xFF, 0xFF, 0x85, 0x00); // #FF8500
            public Color Harmony3   = Color.FromArgb(0xFF, 0xFF, 0xDB, 0x00); // #FFDB00

            public Color GetPartColor(int harmonyIndex, bool isHarmony)
            {
                if (!isHarmony)
                {
                    return LeadVocals;
                }

                return harmonyIndex switch
                {
                    0 => Harmony1,
                    1 => Harmony2,
                    2 => Harmony3,
                    _ => default
                };
            }

            public VocalsColors Copy()
            {
                return (VocalsColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(LeadVocals);
                writer.Write(Harmony1);
                writer.Write(Harmony2);
                writer.Write(Harmony3);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                LeadVocals = reader.ReadColor();
                Harmony1 = reader.ReadColor();
                Harmony2 = reader.ReadColor();
                Harmony3 = reader.ReadColor();
            }
        }
    }
}