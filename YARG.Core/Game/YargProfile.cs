using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Chart;
using YARG.Core.Utility;
using YARG.Core.Extensions;
using System.Linq;
using System.Runtime.Serialization;
using YARG.Core.IO;

namespace YARG.Core.Game
{
    public partial class YargProfile
    {
        private const int PROFILE_VERSION = 8;

        public Guid Id;
        public string Name;

        public bool IsBot;

        public GameMode GameMode;

        public float NoteSpeed;
        public float HighwayLength;

        public bool LeftyFlip;

        public bool RangeEnabled;

        public int FourLaneDrumsHighwayOrderingLength;
        public DrumsHighwayItem[] FourLaneDrumsHighwayOrdering;

        public int ProDrumsHighwayOrderingLength;
        public DrumsHighwayItem[] ProDrumsHighwayOrdering;

        public int FiveLaneDrumsHighwayOrderingLength;
        public DrumsHighwayItem[] FiveLaneDrumsHighwayOrdering;

        public bool UseCymbalModels;

        public OpenLaneDisplayType OpenLaneDisplayType;

        public StarPowerActivationType StarPowerActivationType;

        public int? AutoConnectOrder;

        public long InputCalibrationMilliseconds;
        public double InputCalibrationSeconds
        {
            get => InputCalibrationMilliseconds / 1000.0;
            set => InputCalibrationMilliseconds = (long) (value * 1000);
        }

        public bool HasValidInstrument => GameMode.PossibleInstruments().Contains(CurrentInstrument);

        public Guid EnginePreset;

        public Guid ThemePreset;
        public Guid ColorProfile;
        public Guid CameraPreset;
        public Guid HighwayPreset;
        public Guid RockMeterPreset;

        /// <summary>
        /// The selected instrument.
        /// </summary>
        public Instrument CurrentInstrument;

        /// <summary>
        /// The user's preferred instrument, which may differ from CurrentInstrument when the preferred instrument
        /// is not available for a given chart. This should only be modified when the user explicitly changes their
        /// instrument selection when the previous PreferredInstrument is also available.
        /// </summary>
        public Instrument PreferredInstrument;

        /// <summary>
        /// The selected difficulty.
        /// </summary>
        public Difficulty CurrentDifficulty;

        /// <summary>
        /// The difficulty to be saved in the profile.
        ///
        /// If a song does not contain this difficulty, so long as the player
        /// does not *explicitly* and *manually* change the difficulty, this value
        /// should remain unchanged.
        /// </summary>
        public Difficulty DifficultyFallback;

        [JsonProperty("HarmonyIndex")]
        private byte _harmonyIndex;

        /// <summary>
        /// The harmony index, used for determining what harmony part the player selected.
        /// Does nothing if <see cref="CurrentInstrument"/> is not a harmony.
        /// </summary>
        [JsonIgnore]
        public byte HarmonyIndex
        {
            // Only expose harmony index when playing harmonies, ensures consistent behavior
            // while still allowing harmony index to persist between instrument switches
            get => CurrentInstrument == Instrument.Harmony ? _harmonyIndex : (byte) 0;
            set => _harmonyIndex = value;
        }

        /// <summary>
        /// The currently selected modifiers as a flag.
        /// Use <see cref="AddSingleModifier"/> and <see cref="RemoveModifiers"/> to modify.
        /// </summary>
        [JsonProperty]
        public Modifier CurrentModifiers { get; private set; }

        /// <summary>
        /// The last time this profile was used.
        /// </summary>
        public DateTime LastUsed;

        public YargProfile()
        {
            Id = Guid.NewGuid();
            Name = "Default";
            GameMode = GameMode.FiveFretGuitar;
            NoteSpeed = 6;
            HighwayLength = 1;
            LeftyFlip = false;
            RangeEnabled = true;
            FourLaneDrumsHighwayOrderingLength = 4;
            FourLaneDrumsHighwayOrdering = DEFAULT_FOUR_LANE_ORDERING;
            ProDrumsHighwayOrderingLength = 4;
            ProDrumsHighwayOrdering = DEFAULT_FOUR_LANE_ORDERING;
            FiveLaneDrumsHighwayOrderingLength = 5;
            FiveLaneDrumsHighwayOrdering = DEFAULT_FIVE_LANE_ORDERING;
            UseCymbalModels = true;
            StarPowerActivationType = StarPowerActivationType.RightmostNote;
            OpenLaneDisplayType = OpenLaneDisplayType.Never;

            // Set preset IDs to default
            ColorProfile = Game.ColorProfile.Default.Id;
            CameraPreset = Game.CameraPreset.Default.Id;
            HighwayPreset = Game.HighwayPreset.Default.Id;
            RockMeterPreset = Game.RockMeterPreset.Normal.Id;

            CurrentModifiers = Modifier.None;
        }

        public YargProfile(Guid id) : this()
        {
            Id = id;
        }

        public YargProfile(ref FixedArrayStream stream)
        {
            int version = stream.Read<int>(Endianness.Little);

            Name = stream.ReadString();

            EnginePreset = stream.ReadGuid();

            ThemePreset = stream.ReadGuid();
            ColorProfile = stream.ReadGuid();
            CameraPreset = stream.ReadGuid();

            if (version >= 2)
            {
                HighwayPreset = stream.ReadGuid();
            }
            // This uses CurrentInstrument instead of PreferredInstrument because in replays we only care
            // what instrument is actually being used for the current chart
            CurrentInstrument = (Instrument) stream.ReadByte();
            CurrentDifficulty = (Difficulty) stream.ReadByte();
            CurrentModifiers = (Modifier) stream.Read<ulong>(Endianness.Little);
            _harmonyIndex = stream.ReadByte();

            NoteSpeed = stream.Read<float>(Endianness.Little);
            HighwayLength = stream.Read<float>(Endianness.Little);
            LeftyFlip = stream.ReadBoolean();

            if (version >= 3)
            {
                RangeEnabled = stream.ReadBoolean();
            }

            if (version >= 4)
            {
                UseCymbalModels = stream.ReadBoolean();
                var splitProTomsAndCymbals = stream.ReadBoolean();
                var swapSnareAndHiHat = stream.ReadBoolean();
                var swapCrashAndRide = stream.ReadBoolean();

                if (version < 8) // Interpret the old split-all and single-swap settings into highway orderings
                {
                    FourLaneDrumsHighwayOrderingLength = 4;
                    FourLaneDrumsHighwayOrdering = DEFAULT_FOUR_LANE_ORDERING;

                    ProDrumsHighwayOrderingLength = splitProTomsAndCymbals ? 7 : 4;
                    ProDrumsHighwayOrdering = splitProTomsAndCymbals ? new DrumsHighwayItem[]
                    {
                        swapSnareAndHiHat ?  DrumsHighwayItem.FourLaneYellowCymbal : DrumsHighwayItem.FourLaneRed,
                        swapSnareAndHiHat ?  DrumsHighwayItem.FourLaneRed : DrumsHighwayItem.FourLaneYellowCymbal,
                        DrumsHighwayItem.FourLaneYellowDrum,
                        swapCrashAndRide ? DrumsHighwayItem.FourLaneGreenCymbal : DrumsHighwayItem.FourLaneBlueCymbal,
                        DrumsHighwayItem.FourLaneBlueDrum,
                        swapCrashAndRide ? DrumsHighwayItem.FourLaneBlueCymbal : DrumsHighwayItem.FourLaneGreenCymbal,
                        DrumsHighwayItem.FourLaneGreenDrum,
                    } : DEFAULT_FOUR_LANE_ORDERING;

                    FiveLaneDrumsHighwayOrderingLength = 5;
                    FiveLaneDrumsHighwayOrdering = new DrumsHighwayItem[]
                    {
                        swapSnareAndHiHat ? DrumsHighwayItem.FiveLaneYellow : DrumsHighwayItem.FiveLaneRed,
                        swapSnareAndHiHat ? DrumsHighwayItem.FiveLaneRed : DrumsHighwayItem.FiveLaneYellow,
                        DrumsHighwayItem.FiveLaneBlue,
                        DrumsHighwayItem.FiveLaneOrange,
                        DrumsHighwayItem.FiveLaneGreen
                    };
                }
            } else
            {
                FourLaneDrumsHighwayOrderingLength = 4;
                FourLaneDrumsHighwayOrdering = DEFAULT_FOUR_LANE_ORDERING;
                ProDrumsHighwayOrderingLength = 4;
                ProDrumsHighwayOrdering = DEFAULT_FOUR_LANE_ORDERING;
                FiveLaneDrumsHighwayOrderingLength = 5;
                FiveLaneDrumsHighwayOrdering = DEFAULT_FIVE_LANE_ORDERING;
            }

            if (version >= 5)
            {
                StarPowerActivationType = (StarPowerActivationType) stream.ReadByte();
            }
            else
            {
                StarPowerActivationType = StarPowerActivationType.RightmostNote;
            }

            if (version >= 6)
            {
                GameMode = (GameMode) stream.ReadByte();
            }
            else
            {
                GameMode = CurrentInstrument.ToNativeGameMode();
            }

            if (version >= 7)
            {
                OpenLaneDisplayType = (OpenLaneDisplayType) stream.ReadByte();
            }
            else
            {
                OpenLaneDisplayType = OpenLaneDisplayType.Never;
            }

            if (version >= 8)
            {
                FourLaneDrumsHighwayOrderingLength = stream.ReadByte();
                FourLaneDrumsHighwayOrdering = new DrumsHighwayItem[FourLaneDrumsHighwayOrderingLength];
                for (var i = 0; i < FourLaneDrumsHighwayOrderingLength; i++)
                {
                    FourLaneDrumsHighwayOrdering[i] = (DrumsHighwayItem)stream.ReadByte();
                }

                ProDrumsHighwayOrderingLength = stream.ReadByte();
                ProDrumsHighwayOrdering = new DrumsHighwayItem[ProDrumsHighwayOrderingLength];
                for (var i = 0; i < ProDrumsHighwayOrderingLength; i++)
                {
                    ProDrumsHighwayOrdering[i] = (DrumsHighwayItem) stream.ReadByte();
                }

                FiveLaneDrumsHighwayOrderingLength = stream.ReadByte();
                FiveLaneDrumsHighwayOrdering = new DrumsHighwayItem[FiveLaneDrumsHighwayOrderingLength];
                for (var i = 0; i < FiveLaneDrumsHighwayOrderingLength; i++)
                {
                    FiveLaneDrumsHighwayOrdering[i] = (DrumsHighwayItem) stream.ReadByte();
                }
            }
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

        public void CopyModifiers(YargProfile profile)
        {
            // The modifiers of the other profile are guaranteed to be correct
            CurrentModifiers = profile.CurrentModifiers;
        }

        public void ApplyModifiers<TNote>(InstrumentDifficulty<TNote> track, SyncTrack syncTrack) where TNote : Note<TNote>
        {
            switch (GameMode)
            {
                case GameMode.FiveFretGuitar:
                    if (track is not InstrumentDifficulty<GuitarNote> guitarTrack)
                    {
                        throw new InvalidOperationException("Cannot apply guitar modifiers to non-guitar track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.OpensToGreens))
                    {
                        guitarTrack.ConvertFromOpenToGreen(syncTrack);
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
                    else if (IsModifierActive(Modifier.RangeCompress))
                    {
                        guitarTrack.CompressGuitarRange();
                    }

                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                case GameMode.EliteDrums:
                    if (track is not InstrumentDifficulty<DrumNote> drumsTrack)
                    {
                        throw new InvalidOperationException("Cannot apply drum modifiers to non-drums track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.NoKicks))
                    {
                        drumsTrack.RemoveKickDrumNotes();
                    }

                    if (IsModifierActive(Modifier.NoDynamics))
                    {
                        drumsTrack.RemoveDynamics();
                    }

                    break;
                case GameMode.ProKeys:
                    if (track is InstrumentDifficulty<ProKeysNote> proKeysTrack)
                    {
                        // Apply Pro Keys modifiers (none exist currently)
                        break;
                    }

                    if (track is InstrumentDifficulty<GuitarNote> fiveLaneKeysTrack)
                    {
                        // Apply Five-Lane Keys modifiers
                        if (IsModifierActive(Modifier.RangeCompress))
                        {
                            fiveLaneKeysTrack.CompressGuitarRange();
                        }
                        if (IsModifierActive(Modifier.OpensToGreens))
                        {
                            fiveLaneKeysTrack.ConvertFromOpenToGreen(syncTrack);
                        }
                        break;
                    }

                    else
                    {
                        throw new InvalidOperationException("Cannot apply keys modifiers to non-keys and non-guitar " +
                            $"track with notes of {typeof(TNote)}!");
                    }
                case GameMode.Vocals:
                            throw new InvalidOperationException("For vocals, use ApplyVocalModifiers instead!");
                        }
        }

        public void ApplyVocalModifiers(VocalsPart vocalsPart)
        {
            if (IsModifierActive(Modifier.UnpitchedOnly))
            {
                vocalsPart.ConvertAllToUnpitched();
            }

            if (IsModifierActive(Modifier.NoVocalPercussion))
            {
                vocalsPart.RemovePercussion();
            }
        }

        public void EnsureValidInstrument()
        {
            if (!HasValidInstrument)
            {
                CurrentInstrument = GameMode.PossibleInstruments()[0];
            }

            ValidatePreferredInstrument();
        }

        [OnDeserialized]
        public void ValidateJsonDeserialization(StreamingContext context)
        {
            ValidatePreferredInstrument();
        }

        private void ValidatePreferredInstrument()
        {
            if (!GameMode.PossibleInstruments().Contains(PreferredInstrument))
            {
                PreferredInstrument = HasValidInstrument ? CurrentInstrument : GameMode.PossibleInstruments()[0];
            }
        }

        public void ClaimProfile()
        {
            LastUsed = DateTime.Now;
        }

        // For replay serialization
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PROFILE_VERSION);

            writer.Write(Name);

            writer.Write(EnginePreset);

            writer.Write(ThemePreset);
            writer.Write(ColorProfile);
            writer.Write(CameraPreset);
            writer.Write(HighwayPreset);

            // This uses CurrentInstrument instead of PreferredInstrument because in replays we only care
            // what instrument is actually being used for the current chart
            writer.Write((byte) CurrentInstrument);
            writer.Write((byte) CurrentDifficulty);
            writer.Write((ulong) CurrentModifiers);
            writer.Write(_harmonyIndex);

            writer.Write(NoteSpeed);
            writer.Write(HighwayLength);
            writer.Write(LeftyFlip);

            writer.Write(RangeEnabled);

            writer.Write(UseCymbalModels);
            writer.Write((byte)0); // Superseded by highway orderings
            writer.Write((byte)0); // Superseded by highway orderings
            writer.Write((byte)0); // Superseded by highway orderings

            writer.Write((byte) StarPowerActivationType);

            writer.Write((byte) GameMode);

            writer.Write((byte) OpenLaneDisplayType);

            writer.Write((byte)FourLaneDrumsHighwayOrdering.Length);
            foreach (var item in FourLaneDrumsHighwayOrdering)
            {
                writer.Write((byte) item);
            }

            writer.Write((byte) ProDrumsHighwayOrdering.Length);
            foreach (var item in ProDrumsHighwayOrdering)
            {
                writer.Write((byte) item);
            }

            writer.Write((byte) FiveLaneDrumsHighwayOrdering.Length);
            foreach (var item in FiveLaneDrumsHighwayOrdering)
            {
                writer.Write((byte) item);
            }
        }

        private static DrumsHighwayItem[] DEFAULT_FOUR_LANE_ORDERING = new DrumsHighwayItem[] {
            DrumsHighwayItem.FourLaneRed,
            DrumsHighwayItem.FourLaneYellow,
            DrumsHighwayItem.FourLaneBlue,
            DrumsHighwayItem.FourLaneGreen
        };

        private static DrumsHighwayItem[] DEFAULT_FIVE_LANE_ORDERING = new DrumsHighwayItem[] {
            DrumsHighwayItem.FiveLaneRed,
            DrumsHighwayItem.FiveLaneYellow,
            DrumsHighwayItem.FiveLaneBlue,
            DrumsHighwayItem.FiveLaneOrange,
            DrumsHighwayItem.FiveLaneGreen
        };
    }
}