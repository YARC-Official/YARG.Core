using System;
using System.Buffers.Binary;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public static partial class ReplaySerializer
    {
        private static class Sections
        {
            #region Header

            public static void SerializeHeader(BinaryWriter writer, ReplayHeader header)
            {
                header.Magic.Serialize(writer);

                writer.Write(header.ReplayVersion);
                writer.Write(header.EngineVersion);

                header.ReplayChecksum.Serialize(writer);
            }

            public static ReplayHeader? DeserializeHeader(ref SpanBinaryReader reader, int version = 0)
            {
                var header = new ReplayHeader();

                var magic = new EightCC(reader.ReadBytes(8));

                if (magic != ReplayIO.REPLAY_MAGIC_HEADER)
                {
                    return null;
                }

                header.ReplayVersion = reader.ReadInt16();
                header.EngineVersion = reader.ReadInt16();
                header.ReplayChecksum = HashWrapper.Create(reader.ReadBytes(HashWrapper.HASH_SIZE_IN_BYTES));

                return header;
            }

            #endregion

            #region Metadata

            public static void SerializeMetadata(BinaryWriter writer, ReplayMetadata metadata)
            {
                writer.Write(metadata.SongName);
                writer.Write(metadata.ArtistName);
                writer.Write(metadata.CharterName);
                writer.Write(metadata.BandScore);
                writer.Write((byte) metadata.BandStars);
                writer.Write(metadata.ReplayLength);
                writer.Write(metadata.Date.ToBinary());
                metadata.SongChecksum.Serialize(writer);
            }

            public static ReplayMetadata DeserializeMetadata(ref SpanBinaryReader reader, int version = 0)
            {
                var metadata = new ReplayMetadata();

                metadata.SongName = reader.ReadString();
                metadata.ArtistName = reader.ReadString();
                metadata.CharterName = reader.ReadString();
                metadata.BandScore = reader.ReadInt32();
                metadata.BandStars = (StarAmount) reader.ReadByte();
                metadata.ReplayLength = reader.ReadDouble();
                metadata.Date = DateTime.FromBinary(reader.ReadInt64());
                metadata.SongChecksum = HashWrapper.Create(reader.ReadBytes(HashWrapper.HASH_SIZE_IN_BYTES));

                return metadata;
            }

            #endregion

            #region Preset Container

            public static void SerializePresetContainer(BinaryWriter writer, ReplayPresetContainer presetContainer)
            {
                presetContainer.Serialize(writer);
            }

            public static ReplayPresetContainer DeserializePresetContainer(ref SpanBinaryReader reader, int version = 0)
            {
                var presetContainer = new ReplayPresetContainer();

                presetContainer.Deserialize(ref reader, version);
                return presetContainer;
            }

            #endregion

            #region Frame

            public static void SerializeFrame(BinaryWriter writer, ReplayFrame frame)
            {
                SerializePlayerInfo(writer, frame.PlayerInfo);
                SerializeEngineParameters(writer, frame.EngineParameters, frame.PlayerInfo.Profile.GameMode);
                SerializeStats(writer, frame.Stats, frame.PlayerInfo.Profile.GameMode);

                writer.Write(frame.InputCount);
                foreach (var input in frame.Inputs)
                {
                    writer.Write(input.Time);
                    writer.Write(input.Action);
                    writer.Write(input.Integer);
                }
            }

            public static ReplayFrame DeserializeFrame(ref SpanBinaryReader reader, int version = 0)
            {
                var frame = new ReplayFrame();
                frame.PlayerInfo = DeserializePlayerInfo(ref reader, version);
                frame.EngineParameters =
                    DeserializeEngineParameters(ref reader, frame.PlayerInfo.Profile.GameMode, version);
                frame.Stats = DeserializeStats(ref reader, frame.PlayerInfo.Profile.GameMode, version);

                frame.InputCount = reader.ReadInt32();
                frame.Inputs = new GameInput[frame.InputCount];
                for (int i = 0; i < frame.InputCount; i++)
                {
                    var time = reader.ReadDouble();
                    var action = reader.ReadInt32();
                    var value = reader.ReadInt32();

                    frame.Inputs[i] = new GameInput(time, action, value);
                }

                return frame;
            }

            #endregion

            #region Player Info

            public static void SerializePlayerInfo(BinaryWriter writer, ReplayPlayerInfo playerInfo)
            {
                writer.Write(playerInfo.PlayerId);
                writer.Write(playerInfo.ColorProfileId);
                SerializeYargProfile(writer, playerInfo.Profile);
            }

            public static ReplayPlayerInfo DeserializePlayerInfo(ref SpanBinaryReader reader, int version = 0)
            {
                var playerInfo = new ReplayPlayerInfo();
                playerInfo.PlayerId = reader.ReadInt32();
                playerInfo.ColorProfileId = reader.ReadInt32();

                playerInfo.Profile = DeserializeYargProfile(ref reader, version);

                return playerInfo;
            }

            #endregion

            #region Profile

            public static void SerializeYargProfile(BinaryWriter writer, YargProfile profile)
            {
                writer.Write(YargProfile.PROFILE_VERSION);

                writer.Write(profile.Id);
                writer.Write(profile.Name);
                writer.Write(profile.EnginePreset);
                writer.Write(profile.ThemePreset);
                writer.Write(profile.ColorProfile);
                writer.Write(profile.CameraPreset);
                writer.Write((byte) profile.CurrentInstrument);
                writer.Write((byte) profile.CurrentDifficulty);
                writer.Write((ulong) profile.CurrentModifiers);
                writer.Write(profile.HarmonyIndex);
                writer.Write(profile.NoteSpeed);
                writer.Write(profile.HighwayLength);
                writer.Write(profile.LeftyFlip);
            }

            public static YargProfile DeserializeYargProfile(ref SpanBinaryReader reader, int version = 0)
            {
                var profile = new YargProfile();

                version = reader.ReadInt32();

                profile.Id = reader.ReadGuid();

                profile.Name = reader.ReadString();

                profile.EnginePreset = reader.ReadGuid();

                profile.ThemePreset = reader.ReadGuid();
                profile.ColorProfile = reader.ReadGuid();
                profile.CameraPreset = reader.ReadGuid();

                profile.CurrentInstrument = (Instrument) reader.ReadByte();
                profile.CurrentDifficulty = (Difficulty) reader.ReadByte();
                profile.CurrentModifiers = (Modifier) reader.ReadUInt64();
                profile.HarmonyIndex = reader.ReadByte();

                profile.NoteSpeed = reader.ReadSingle();
                profile.HighwayLength = reader.ReadSingle();
                profile.LeftyFlip = reader.ReadBoolean();

                profile.GameMode = profile.CurrentInstrument.ToGameMode();

                return profile;
            }

            #endregion

            #region Engine Parameters

            public static void SerializeEngineParameters(BinaryWriter writer, BaseEngineParameters engineParameters,
                GameMode gameMode)
            {
                writer.Write(engineParameters.HitWindow.MaxWindow);
                writer.Write(engineParameters.HitWindow.MinWindow);
                writer.Write(engineParameters.HitWindow.FrontToBackRatio);

                writer.Write(engineParameters.HitWindow.IsDynamic);
                writer.Write(engineParameters.HitWindow.DynamicWindowSlope);
                writer.Write(engineParameters.HitWindow.DynamicWindowScale);
                writer.Write(engineParameters.HitWindow.DynamicWindowGamma);

                writer.Write(engineParameters.MaxMultiplier);
                writer.Write(engineParameters.StarPowerWhammyBuffer);
                writer.Write(engineParameters.StarMultiplierThresholds.Length);
                foreach (var f in engineParameters.StarMultiplierThresholds)
                {
                    writer.Write(f);
                }

                writer.Write(engineParameters.SongSpeed);

                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        Instruments.SerializeGuitarParameters(writer, (engineParameters as GuitarEngineParameters)!);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        Instruments.SerializeDrumsParameters(writer, (engineParameters as DrumsEngineParameters)!);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        Instruments.SerializeProKeysParameters(writer, (engineParameters as ProKeysEngineParameters)!);
                        break;
                    case GameMode.Vocals:
                        Instruments.SerializeVocalsParameters(writer, (engineParameters as VocalsEngineParameters)!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public static BaseEngineParameters DeserializeEngineParameters(ref SpanBinaryReader reader, GameMode gameMode,
                int version = 0)
            {
                // Hit Window
                var maxWindow = reader.ReadDouble();
                var minWindow = reader.ReadDouble();
                var frontToBackRatio = reader.ReadDouble();
                var isDynamic = reader.ReadBoolean();
                var dwSlope = reader.ReadDouble();
                var dwScale = reader.ReadDouble();
                var dwGamma = reader.ReadDouble();

                int maxMultiplier = reader.ReadInt32();
                double whammyBuffer = reader.ReadDouble();
                int starThresholdsLength = reader.ReadInt32();

                float[] starMultiplierThresholds = new float[starThresholdsLength];
                for (int i = 0; i < starMultiplierThresholds.Length; i++)
                {
                    starMultiplierThresholds[i] = reader.ReadSingle();
                }

                double songSpeed = reader.ReadDouble();

                BaseEngineParameters engineParameters = null!;
                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        engineParameters = Instruments.DeserializeGuitarParameters(ref reader, version);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        engineParameters = Instruments.DeserializeDrumsParameters(ref reader, version);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        engineParameters = Instruments.DeserializeProKeysParameters(ref reader, version);
                        break;
                    case GameMode.Vocals:
                        engineParameters = Instruments.DeserializeVocalsParameters(ref reader, version);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
                }

                var hitWindow = new HitWindowSettings(maxWindow, minWindow, frontToBackRatio, isDynamic, dwSlope, dwScale, dwGamma);

                engineParameters.HitWindow = hitWindow;
                engineParameters.MaxMultiplier = maxMultiplier;
                engineParameters.StarMultiplierThresholds = starMultiplierThresholds;
                engineParameters.StarPowerWhammyBuffer = whammyBuffer;
                engineParameters.SongSpeed = songSpeed;

                return engineParameters;
            }

            #endregion

            #region Stats

            public static void SerializeStats(BinaryWriter writer, BaseStats stats, GameMode gameMode)
            {
                writer.Write(stats.CommittedScore);
                writer.Write(stats.PendingScore);
                writer.Write(stats.SustainScore);
                writer.Write(stats.Combo);
                writer.Write(stats.MaxCombo);
                writer.Write(stats.ScoreMultiplier);
                writer.Write(stats.NotesHit);
                writer.Write(stats.TotalNotes);
                writer.Write(stats.StarPowerTickAmount);
                writer.Write(stats.TotalStarPowerTicks);
                writer.Write(stats.TimeInStarPower);
                writer.Write(stats.StarPowerWhammyTicks);
                writer.Write(stats.IsStarPowerActive);
                writer.Write(stats.StarPowerPhrasesHit);
                writer.Write(stats.TotalStarPowerPhrases);
                writer.Write(stats.SoloBonuses);
                writer.Write(stats.StarPowerScore);

                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        Instruments.SerializeGuitarStats(writer, (stats as GuitarStats)!);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        Instruments.SerializeDrumsStats(writer, (stats as DrumsStats)!);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        Instruments.SerializeProKeysStats(writer, (stats as ProKeysStats)!);
                        break;
                    case GameMode.Vocals:
                        Instruments.SerializeVocalsStats(writer, (stats as VocalsStats)!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
                }
            }

            public static BaseStats DeserializeStats(ref SpanBinaryReader reader, GameMode gameMode, int version = 0)
            {
                var committedScore = reader.ReadInt32();
                var pendingScore = reader.ReadInt32();
                var sustainScore = reader.ReadInt32();
                var combo = reader.ReadInt32();
                var maxCombo = reader.ReadInt32();
                var scoreMultiplier = reader.ReadInt32();
                var notesHit = reader.ReadInt32();
                var totalNotes = reader.ReadInt32();
                var starPowerTickAmount = reader.ReadUInt32();
                var totalStarPowerTicks = reader.ReadUInt32();
                var timeInStarPower = reader.ReadDouble();
                var whammyTicks = reader.ReadUInt32();
                var isStarPowerActive = reader.ReadBoolean();
                var starPowerPhrasesHit = reader.ReadInt32();
                var totalStarPowerPhrases = reader.ReadInt32();
                var soloBonuses = reader.ReadInt32();
                var starPowerScore = reader.ReadInt32();

                BaseStats stats = null!;
                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        stats = Instruments.DeserializeGuitarStats(ref reader, version);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        stats = Instruments.DeserializeDrumsStats(ref reader, version);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        stats = Instruments.DeserializeProKeysStats(ref reader, version);
                        break;
                    case GameMode.Vocals:
                        stats = Instruments.DeserializeVocalsStats(ref reader, version);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
                }

                stats.CommittedScore = committedScore;
                stats.PendingScore = pendingScore;
                stats.SustainScore = sustainScore;
                stats.Combo = combo;
                stats.MaxCombo = maxCombo;
                stats.ScoreMultiplier = scoreMultiplier;
                stats.NotesHit = notesHit;
                stats.TotalNotes = totalNotes;
                stats.StarPowerTickAmount = starPowerTickAmount;
                stats.TotalStarPowerTicks = totalStarPowerTicks;
                stats.TimeInStarPower = timeInStarPower;
                stats.StarPowerWhammyTicks = whammyTicks;
                stats.IsStarPowerActive = isStarPowerActive;
                stats.StarPowerPhrasesHit = starPowerPhrasesHit;
                stats.TotalStarPowerPhrases = totalStarPowerPhrases;
                stats.SoloBonuses = soloBonuses;
                stats.StarPowerScore = starPowerScore;

                return stats;
            }

            #endregion
        }
    }
}