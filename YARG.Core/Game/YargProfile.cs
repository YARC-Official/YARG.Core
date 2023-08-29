using System;
using Newtonsoft.Json;

namespace YARG.Core.Game
{
    public class YargProfile
    {
        public Guid Id;

        public string Name;

        public GameMode GameMode;

        [JsonIgnore]
        public Instrument Instrument;

        [JsonIgnore]
        public Difficulty Difficulty;

        public float NoteSpeed;
        public float HighwayLength;

        public bool LeftyFlip;

        public bool IsBot;

        public Modifier Modifiers { get; private set; }

        public YargProfile()
        {
            Id = Guid.NewGuid();
            Name = "Default";
            GameMode = GameMode.FiveFretGuitar;
            Instrument = Instrument.FiveFretGuitar;
            Difficulty = Difficulty.Expert;
            NoteSpeed = 6;
            HighwayLength = 1;
            LeftyFlip = false;

            Modifiers = Modifier.None;
        }

        public YargProfile(Guid id) : this()
        {
            Id = id;
        }

        public void ApplyModifiers(Modifier modifier)
        {
            // These modifiers conflict so we need to remove the conflicting modifiers if one is applied
            switch (modifier)
            {
                case Modifier.AllStrums:
                    RemoveModifiers(Modifier.AllHopos | Modifier.AllTaps);
                    break;
                case Modifier.AllHopos:
                    RemoveModifiers(Modifier.AllStrums | Modifier.AllTaps);
                    break;
                case Modifier.AllTaps:
                    RemoveModifiers(Modifier.AllStrums | Modifier.AllHopos);
                    break;
                case Modifier.HoposToTaps:
                    RemoveModifiers(Modifier.AllStrums | Modifier.AllHopos | Modifier.AllTaps);
                    break;
            }

            Modifiers |= modifier;
        }

        public void RemoveModifiers(Modifier modifier)
        {
            Modifiers &= ~modifier;
        }

        public bool IsModifierActive(Modifier modifier)
        {
            return (Modifiers & modifier) == modifier;
        }
    }
}