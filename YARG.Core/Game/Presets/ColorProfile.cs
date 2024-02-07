using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile : BasePreset
    {
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

        public FiveFretGuitarColors FiveFretGuitar;
        public FourLaneDrumsColors  FourLaneDrums;
        public FiveLaneDrumsColors  FiveLaneDrums;

        public ColorProfile(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarColors();
            FourLaneDrums = new FourLaneDrumsColors();
            FiveLaneDrums = new FiveLaneDrumsColors();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ColorProfile(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                FourLaneDrums = FourLaneDrums.Copy(),
                FiveLaneDrums = FiveLaneDrums.Copy()
            };
        }
    }
}