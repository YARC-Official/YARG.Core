﻿using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Chart;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public class YargProfile : IBinarySerializable
    {
        private const int PROFILE_VERSION = 1;

        public Guid Id;
        public string Name;

        public bool IsBot;

        public GameMode GameMode;

        public float NoteSpeed;
        public float HighwayLength;

        public bool LeftyFlip;

        public Guid ColorProfile;
        public Guid CameraPreset;

        [JsonIgnore]
        public Instrument Instrument;

        [JsonIgnore]
        public Difficulty Difficulty;

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

            // Set preset IDs to default
            ColorProfile = Game.ColorProfile.Default.Id;
            CameraPreset = Game.CameraPreset.Default.Id;

            Modifiers = Modifier.None;
        }

        public YargProfile(Guid id) : this()
        {
            Id = id;
        }

        public void AddModifiers(Modifier modifier)
        {
            // These modifiers conflict so we need to remove the conflicting modifiers if one is applied
            switch (modifier)
            {
                case Modifier.AllStrums:
                    RemoveModifiers(Modifier.AllHopos | Modifier.AllTaps | Modifier.HoposToTaps);
                    break;
                case Modifier.AllHopos:
                    RemoveModifiers(Modifier.AllStrums | Modifier.AllTaps | Modifier.HoposToTaps);
                    break;
                case Modifier.AllTaps:
                    RemoveModifiers(Modifier.AllStrums | Modifier.AllHopos | Modifier.HoposToTaps);
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

        public void ApplyModifiers<TNote>(InstrumentDifficulty<TNote> track) where TNote : Note<TNote>
        {
            switch (Instrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                    if (track is not InstrumentDifficulty<GuitarNote> guitarTrack)
                        throw new InvalidOperationException($"Cannot apply guitar modifiers to non-guitar track with notes of {typeof(TNote)}!");

                    if(IsModifierActive(Modifier.AllStrums)) guitarTrack.ConvertToGuitarType(GuitarNoteType.Strum);
                    else if(IsModifierActive(Modifier.AllHopos)) guitarTrack.ConvertToGuitarType(GuitarNoteType.Hopo);
                    else if(IsModifierActive(Modifier.AllTaps)) guitarTrack.ConvertToGuitarType(GuitarNoteType.Tap);
                    else if(IsModifierActive(Modifier.HoposToTaps)) guitarTrack.ConvertHoposToTaps();
                    break;
            }
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
            writer.Write((ulong)Modifiers);
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
            Modifiers = (Modifier)reader.ReadUInt64();

            GameMode = Instrument.ToGameMode();
        }
    }
}