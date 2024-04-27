using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile : BasePreset, IBinarySerializable
    {
        private const int COLOR_PROFILE_VERSION = 1;

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

        public FiveFretGuitarColors FiveFretGuitar;
        public FourLaneDrumsColors  FourLaneDrums;
        public FiveLaneDrumsColors  FiveLaneDrums;

        public ColorProfile(string name, bool defaultPreset)
            : this(name, defaultPreset, in FiveFretGuitarColors.Default, in FourLaneDrumsColors.Default, in FiveLaneDrumsColors.Default)
        {
        }

        private ColorProfile(string name, bool defaultPreset, in FiveFretGuitarColors fivefret, in FourLaneDrumsColors fourlane, in FiveLaneDrumsColors fivelane)
            : base(name, defaultPreset)
        {
            FiveFretGuitar = fivefret;
            FourLaneDrums = fourlane;
            FiveLaneDrums = fivelane;
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ColorProfile(name, false, in FiveFretGuitar, in FourLaneDrums, in FiveLaneDrums);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Name);

            FiveFretGuitar.Serialize(writer);
            FourLaneDrums.Serialize(writer);
            FiveLaneDrums.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();
            Name = reader.ReadString();

            FiveFretGuitar.Deserialize(reader, version);
            FourLaneDrums.Deserialize(reader, version);
            FiveLaneDrums.Deserialize(reader, version);
        }
    }
}