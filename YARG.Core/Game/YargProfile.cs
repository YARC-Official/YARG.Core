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

        public long InputCalibrationMilliseconds;
        public double InputCalibrationSeconds
        {
            get => InputCalibrationMilliseconds / 1000.0;
            set => InputCalibrationMilliseconds = (long) (value * 1000);
        }

        public Guid ColorProfile;
        public Guid CameraPreset;

        /// <summary>
        /// The selected instrument.
        /// </summary>
        public Instrument CurrentInstrument;

        /// <summary>
        /// The selected difficulty.
        /// </summary>
        public Difficulty CurrentDifficulty;

        /// <summary>
        /// The harmony index, used for determining what harmony part the player selected.
        /// Does nothing if <see cref="CurrentInstrument"/> is not a harmony.
        /// </summary>
        public byte HarmonyIndex;

        /// <summary>
        /// The currently selected modifiers as a flag.
        /// Use <see cref="AddSingleModifier"/> and <see cref="RemoveModifiers"/> to modify.
        /// </summary>
        [JsonProperty]
        public Modifier CurrentModifiers { get; private set; }

        public YargProfile()
        {
            Id = Guid.NewGuid();
            Name = "Default";
            GameMode = GameMode.FiveFretGuitar;
            NoteSpeed = 6;
            HighwayLength = 1;
            LeftyFlip = false;

            // Set preset IDs to default
            ColorProfile = Game.ColorProfile.Default.Id;
            CameraPreset = Game.CameraPreset.Default.Id;

            CurrentModifiers = Modifier.None;
        }

        public YargProfile(Guid id) : this()
        {
            Id = id;
        }

        public void AddSingleModifier(Modifier modifier)
        {
            // Remove conflicting modifiers first
            RemoveModifiers(ModifierConflicts.FromSingleModifier(modifier));
            CurrentModifiers |= modifier;
        }

        public void RemoveModifiers(Modifier modifier)
        {
            CurrentModifiers &= ~modifier;
        }

        public bool IsModifierActive(Modifier modifier)
        {
            return (CurrentModifiers & modifier) == modifier;
        }

        public void ApplyModifiers<TNote>(InstrumentDifficulty<TNote> track) where TNote : Note<TNote>
        {
            switch (CurrentInstrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                    if (track is not InstrumentDifficulty<GuitarNote> guitarTrack)
                    {
                        throw new InvalidOperationException($"Cannot apply guitar modifiers to non-guitar track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.AllStrums))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Strum);
                    }
                    else if (IsModifierActive(Modifier.AllHopos))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Hopo);
                    }
                    else if (IsModifierActive(Modifier.AllTaps))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Tap);
                    }
                    else if (IsModifierActive(Modifier.HoposToTaps))
                    {
                        guitarTrack.ConvertFromTypeToType(GuitarNoteType.Hopo, GuitarNoteType.Tap);
                    }
                    else if (IsModifierActive(Modifier.TapsToHopos))
                    {
                        guitarTrack.ConvertFromTypeToType(GuitarNoteType.Tap, GuitarNoteType.Hopo);
                    }

                    break;
                case GameMode.FourLaneDrums:
                    if (track is not InstrumentDifficulty<DrumNote> drumsTrack)
                    {
                        throw new InvalidOperationException($"Cannot apply guitar modifiers to non-drums track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.NoKicks))
                    {
                        // TODO
                    }

                    break;
            }
        }

        // For replay serialization
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PROFILE_VERSION);

            writer.Write(Name);

            writer.Write((byte) CurrentInstrument);
            writer.Write((byte) CurrentDifficulty);
            writer.Write((ulong) CurrentModifiers);
            writer.Write(HarmonyIndex);

            writer.Write(NoteSpeed);
            writer.Write(HighwayLength);
            writer.Write(LeftyFlip);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();

            Name = reader.ReadString();

            CurrentInstrument = (Instrument) reader.ReadByte();
            CurrentDifficulty = (Difficulty) reader.ReadByte();
            CurrentModifiers = (Modifier) reader.ReadUInt64();
            HarmonyIndex = reader.ReadByte();

            NoteSpeed = reader.ReadSingle();
            HighwayLength = reader.ReadSingle();
            LeftyFlip = reader.ReadBoolean();

            GameMode = CurrentInstrument.ToGameMode();
        }
    }
}