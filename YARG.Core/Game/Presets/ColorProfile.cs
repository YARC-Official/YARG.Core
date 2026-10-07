using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Extensions;
using YARG.Core.Game.Settings;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile : BasePreset, IBinarySerializable
    {
        private const int COLOR_PROFILE_VERSION = 3;

        /// <summary>
        /// Interface that has methods that allows for generic fret color retrieval.
        /// Not all instruments have frets, so it's an interface.
        /// </summary>
        public interface IFretColorProvider
        {
            public Color GetFretColor(int index);
            public Color GetFretInnerColor(int index);
            public Color GetParticleColor(int index);
        }

        [JsonIgnore]
        public int Version = COLOR_PROFILE_VERSION;

        [SettingSubSection]
        public FiveFretGuitarColors FiveFretGuitar;
        [SettingSubSection]
        public SixFretGuitarColors SixFretGuitar;
        [SettingSubSection]
        public FourLaneDrumsColors FourLaneDrums;
        [SettingSubSection]
        public FiveLaneDrumsColors FiveLaneDrums;
        [SettingSubSection]
        public ProKeysColors ProKeys;
        [SettingSubSection]
        public VocalsColors Vocals;

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

        public ColorProfile(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarColors();
            SixFretGuitar = new SixFretGuitarColors();
            FourLaneDrums = new FourLaneDrumsColors();
            FiveLaneDrums = new FiveLaneDrumsColors();
            ProKeys = new ProKeysColors();
            Vocals = new VocalsColors();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ColorProfile(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                SixFretGuitar = SixFretGuitar.Copy(),
                FourLaneDrums = FourLaneDrums.Copy(),
                FiveLaneDrums = FiveLaneDrums.Copy(),
                ProKeys = ProKeys.Copy(),
                Vocals = Vocals.Copy(),
            };
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Name);

            FiveFretGuitar.Serialize(writer);
            SixFretGuitar.Serialize(writer);
            FourLaneDrums.Serialize(writer);
            FiveLaneDrums.Serialize(writer);
            ProKeys.Serialize(writer);
            Vocals.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();
            Name = reader.ReadString();

            FiveFretGuitar.Deserialize(reader, version);
            SixFretGuitar.Deserialize(reader, version);
            FourLaneDrums.Deserialize(reader, version);
            FiveLaneDrums.Deserialize(reader, version);
            ProKeys.Deserialize(reader, version);

            if (version >= 3)
            {
                Vocals.Deserialize(reader, version);
            }
        }
    }
}
