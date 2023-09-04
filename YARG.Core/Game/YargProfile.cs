using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public class YargProfile : IBinarySerializable
    {
        private const int PROFILE_VERSION = 1;

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

        [JsonIgnore]
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

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PROFILE_VERSION);

            writer.Write(Name);
            writer.Write((byte) Instrument);
            writer.Write((byte) Difficulty);
            writer.Write(NoteSpeed);
            writer.Write(HighwayLength);
            writer.Write(LeftyFlip);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();

            Name = reader.ReadString();
            Instrument = (Instrument) reader.ReadByte();
            Difficulty = (Difficulty) reader.ReadByte();
            NoteSpeed = reader.ReadSingle();
            HighwayLength = reader.ReadSingle();
            LeftyFlip = reader.ReadBoolean();

            GameMode = Instrument.ToGameMode();
        }
    }
}