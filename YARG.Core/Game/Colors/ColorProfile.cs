using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile : IBinarySerializable
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

        #region Default Colors

        private static readonly Color DefaultPurpleFret = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenFret  = Color.FromArgb(0xFF, 0x54, 0x98, 0x03); // #549803
        private static readonly Color DefaultRedFret    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x07); // #ff0007
        private static readonly Color DefaultYellowFret = Color.FromArgb(0xFF, 0xFF, 0xE9, 0x00); // #ffe900
        private static readonly Color DefaultBlueFret   = Color.FromArgb(0xFF, 0x00, 0x72, 0x98); // #007298
        private static readonly Color DefaultOrangeFret = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // #ff4300

        private static readonly Color DefaultPurpleFretInner = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenFretInner  = Color.FromArgb(0xFF, 0x5F, 0xA4, 0x01); // #5fa401
        private static readonly Color DefaultRedFretInner    = Color.FromArgb(0xFF, 0xC8, 0x00, 0x1D); // #c8001d
        private static readonly Color DefaultYellowFretInner = Color.FromArgb(0xFF, 0xCA, 0xBE, 0x00); // #cabe00
        private static readonly Color DefaultBlueFretInner   = Color.FromArgb(0xFF, 0x01, 0x7E, 0xBB); // #017ebb
        private static readonly Color DefaultOrangeFretInner = Color.FromArgb(0xFF, 0xCA, 0x50, 0x00); // #ca5000

        private static readonly Color DefaultPurpleNote = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenNote  = Color.FromArgb(0xFF, 0x85, 0xE5, 0x00); // #85e500
        private static readonly Color DefaultRedNote    = Color.FromArgb(0xFF, 0xFF, 0x03, 0x00); // #ff0300
        private static readonly Color DefaultYellowNote = Color.FromArgb(0xFF, 0xFF, 0xE5, 0x00); // #ffe500
        private static readonly Color DefaultBlueNote   = Color.FromArgb(0xFF, 0x00, 0x5D, 0xFF); // #005dff
        private static readonly Color DefaultOrangeNote = Color.FromArgb(0xFF, 0xFF, 0x43, 0x00); // #ff4300

        private static readonly Color DefaultStarpower  = Color.White; // #ffffff

        private static readonly Color DefaultPurpleParticles = Color.FromArgb(0xFF, 0xC8, 0x00, 0xFF); // #c800ff
        private static readonly Color DefaultGreenParticles  = Color.FromArgb(0xFF, 0x94, 0xFF, 0x00); // #94ff00
        private static readonly Color DefaultRedParticles    = Color.FromArgb(0xFF, 0xFF, 0x00, 0x26); // #ff0026
        private static readonly Color DefaultYellowParticles = Color.FromArgb(0xFF, 0xFF, 0xF0, 0x00); // #fff000
        private static readonly Color DefaultBlueParticles   = Color.FromArgb(0xFF, 0x00, 0xAB, 0xFF); // #00abff
        private static readonly Color DefaultOrangeParticles = Color.FromArgb(0xFF, 0xFF, 0x65, 0x00); // #ff6500

        #endregion

        public static ColorProfile Default = new("Default");

        [JsonIgnore]
        public int Version = COLOR_PROFILE_VERSION;

        public string Name;

        public FiveFretGuitarColors FiveFretGuitar;
        public FourLaneDrumsColors  FourLaneDrums;

        public ColorProfile(string name)
        {
            Name = name;

            FiveFretGuitar = new FiveFretGuitarColors();
            FourLaneDrums = new FourLaneDrumsColors();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Name);

            FiveFretGuitar.Serialize(writer);
            FourLaneDrums.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();

            Name = reader.ReadString();

            FiveFretGuitar.Deserialize(reader, version);
            FourLaneDrums.Deserialize(reader, version);
        }
    }
}