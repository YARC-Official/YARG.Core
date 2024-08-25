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

                header.ReplayVersion = reader.ReadInt32();
                header.EngineVersion = reader.ReadInt32();
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

            public static ReplayMetadata DeserializeMetadata(UnmanagedMemoryStream stream, int version = 0)
            {
                using var blockStream = ReadBlock(stream);

                var metadata = new ReplayMetadata();

                metadata.SongName = blockStream.ReadString();
                metadata.ArtistName = blockStream.ReadString();
                metadata.CharterName = blockStream.ReadString();
                metadata.BandScore = blockStream.Read<int>(Endianness.Little);
                metadata.BandStars = (StarAmount) blockStream.ReadByte();
                metadata.ReplayLength = blockStream.Read<double>(Endianness.Little);
                metadata.Date = DateTime.FromBinary(blockStream.Read<long>(Endianness.Little));
                metadata.SongChecksum = HashWrapper.Deserialize(blockStream);

                return metadata;
            }

            #endregion

            #region Preset Container

            public static void SerializePresetContainer(BinaryWriter writer, ReplayPresetContainer presetContainer)
            {
                presetContainer.Serialize(writer);
            }

            public static ReplayPresetContainer DeserializePresetContainer(UnmanagedMemoryStream stream, int version = 0)
            {
                using var blockStream = ReadBlock(stream);

                var presetContainer = new ReplayPresetContainer();

                presetContainer.Deserialize(blockStream, version);
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

            public static ReplayFrame DeserializeFrame(UnmanagedMemoryStream stream, int version = 0)
            {
                using var blockStream = ReadBlock(stream);

                var frame = new ReplayFrame();
                frame.PlayerInfo = DeserializePlayerInfo(blockStream, version);
                frame.EngineParameters =
                    DeserializeEngineParameters(blockStream, frame.PlayerInfo.Profile.GameMode, version);
                frame.Stats = DeserializeStats(blockStream, frame.PlayerInfo.Profile.GameMode, version);

                frame.InputCount = blockStream.Read<int>(Endianness.Little);
                frame.Inputs = new GameInput[frame.InputCount];
                for (int i = 0; i < frame.InputCount; i++)
                {
                    var time = blockStream.Read<double>(Endianness.Little);
                    var action = blockStream.Read<int>(Endianness.Little);
                    var value = blockStream.Read<int>(Endianness.Little);

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

            public static ReplayPlayerInfo DeserializePlayerInfo(UnmanagedMemoryStream stream, int version = 0)
            {
                var playerInfo = new ReplayPlayerInfo();
                playerInfo.PlayerId = stream.Read<int>(Endianness.Little);
                playerInfo.ColorProfileId = stream.Read<int>(Endianness.Little);

                playerInfo.Profile = DeserializeYargProfile(stream, version);

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

            public static YargProfile DeserializeYargProfile(UnmanagedMemoryStream stream, int version = 0)
            {
                var profile = new YargProfile();

                version = stream.Read<int>(Endianness.Little);

                profile.Id = stream.ReadGuid();

                profile.Name = stream.ReadString();

                profile.EnginePreset = stream.ReadGuid();

                profile.ThemePreset = stream.ReadGuid();
                profile.ColorProfile = stream.ReadGuid();
                profile.CameraPreset = stream.ReadGuid();

                profile.CurrentInstrument = (Instrument) stream.ReadByte();
                profile.CurrentDifficulty = (Difficulty) stream.ReadByte();
                profile.CurrentModifiers = (Modifier) stream.Read<ulong>(Endianness.Little);
                profile.HarmonyIndex = (byte) stream.ReadByte();

                profile.NoteSpeed = stream.Read<float>(Endianness.Little);
                profile.HighwayLength = stream.Read<float>(Endianness.Little);
                profile.LeftyFlip = stream.ReadBoolean();

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

            public static BaseEngineParameters DeserializeEngineParameters(UnmanagedMemoryStream stream, GameMode gameMode,
                int version = 0)
            {
                // Hit Window
                var maxWindow = stream.Read<double>(Endianness.Little);
                var minWindow = stream.Read<double>(Endianness.Little);
                var frontToBackRatio = stream.Read<double>(Endianness.Little);
                var isDynamic = stream.ReadBoolean();
                var dwSlope = stream.Read<double>(Endianness.Little);
                var dwScale = stream.Read<double>(Endianness.Little);
                var dwGamma = stream.Read<double>(Endianness.Little);

                int maxMultiplier = stream.Read<int>(Endianness.Little);
                double whammyBuffer = stream.Read<double>(Endianness.Little);
                int starThresholdsLength = stream.Read<int>(Endianness.Little);

                float[] starMultiplierThresholds = new float[starThresholdsLength];
                for (int i = 0; i < starMultiplierThresholds.Length; i++)
                {
                    starMultiplierThresholds[i] = stream.Read<float>(Endianness.Little);
                }

                double songSpeed = stream.Read<double>(Endianness.Little);

                BaseEngineParameters engineParameters = null!;
                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        engineParameters = Instruments.DeserializeGuitarParameters(stream, version);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        engineParameters = Instruments.DeserializeDrumsParameters(stream, version);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        engineParameters = Instruments.DeserializeProKeysParameters(stream, version);
                        break;
                    case GameMode.Vocals:
                        engineParameters = Instruments.DeserializeVocalsParameters(stream, version);
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

            public static BaseStats DeserializeStats(UnmanagedMemoryStream stream, GameMode gameMode, int version = 0)
            {
                var committedScore = stream.Read<int>(Endianness.Little);
                var pendingScore = stream.Read<int>(Endianness.Little);
                var sustainScore = stream.Read<int>(Endianness.Little);
                var combo = stream.Read<int>(Endianness.Little);
                var maxCombo = stream.Read<int>(Endianness.Little);
                var scoreMultiplier = stream.Read<int>(Endianness.Little);
                var notesHit = stream.Read<int>(Endianness.Little);
                var totalNotes = stream.Read<int>(Endianness.Little);
                var starPowerTickAmount = stream.Read<uint>(Endianness.Little);
                var totalStarPowerTicks = stream.Read<uint>(Endianness.Little);
                var timeInStarPower = stream.Read<double>(Endianness.Little);
                var whammyTicks = stream.Read<uint>(Endianness.Little);
                var isStarPowerActive = stream.ReadBoolean();
                var starPowerPhrasesHit = stream.Read<int>(Endianness.Little);
                var totalStarPowerPhrases = stream.Read<int>(Endianness.Little);
                var soloBonuses = stream.Read<int>(Endianness.Little);
                var starPowerScore = stream.Read<int>(Endianness.Little);

                BaseStats stats = null!;
                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        stats = Instruments.DeserializeGuitarStats(stream, version);
                        break;
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        stats = Instruments.DeserializeDrumsStats(stream, version);
                        break;
                    case GameMode.ProGuitar:
                        break;
                    case GameMode.ProKeys:
                        stats = Instruments.DeserializeProKeysStats(stream, version);
                        break;
                    case GameMode.Vocals:
                        stats = Instruments.DeserializeVocalsStats(stream, version);
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