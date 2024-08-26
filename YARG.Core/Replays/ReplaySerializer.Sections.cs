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
using YARG.Core.Replays.Serialization;
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

                var playerInfo = DeserializePlayerInfo(blockStream, version);
                var engineParameters =
                    DeserializeEngineParameters(blockStream, playerInfo.Profile.GameMode, version);
                var stats = DeserializeStats(blockStream, playerInfo.Profile.GameMode, version);

                var inputCount = blockStream.Read<int>(Endianness.Little);
                var inputs = new GameInput[inputCount];
                for (int i = 0; i < inputCount; i++)
                {
                    var time = blockStream.Read<double>(Endianness.Little);
                    var action = blockStream.Read<int>(Endianness.Little);
                    var value = blockStream.Read<int>(Endianness.Little);

                    inputs[i] = new GameInput(time, action, value);
                }

                return new ReplayFrame(playerInfo, engineParameters, stats, inputs);
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
                var hitWindow = new SerializedHitWindowSettings();
                var baseParams = new SerializedBaseEngineParameters();

                // Hit Window
                hitWindow.MaxWindow = stream.Read<double>(Endianness.Little);
                hitWindow.MinWindow = stream.Read<double>(Endianness.Little);
                hitWindow.FrontToBackRatio = stream.Read<double>(Endianness.Little);
                hitWindow.IsDynamic = stream.ReadBoolean();
                hitWindow.DynamicWindowSlope = stream.Read<double>(Endianness.Little);
                hitWindow.DynamicWindowScale = stream.Read<double>(Endianness.Little);
                hitWindow.DynamicWindowGamma = stream.Read<double>(Endianness.Little);

                // Base Engine Params
                baseParams.MaxMultiplier = stream.Read<int>(Endianness.Little);
                baseParams.StarPowerWhammyBuffer = stream.Read<double>(Endianness.Little);
                baseParams.SustainDropLeniency = stream.Read<double>(Endianness.Little);
                baseParams.StarMultiplierThresholds = new float[stream.Read<int>(Endianness.Little)];

                for (int i = 0; i < baseParams.StarMultiplierThresholds.Length; i++)
                {
                    baseParams.StarMultiplierThresholds[i] = stream.Read<float>(Endianness.Little);
                }

                baseParams.SongSpeed = stream.Read<double>(Endianness.Little);

                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        var guitarParams = Instruments.DeserializeGuitarParameters(stream, version);
                        return new GuitarEngineParameters(guitarParams, baseParams);
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        var drumsParams = Instruments.DeserializeDrumsParameters(stream, version);
                        return new DrumsEngineParameters(drumsParams, baseParams);
                    case GameMode.ProGuitar:
                        throw new NotImplementedException("Pro Guitar Replays are not implemented yet!");
                    case GameMode.ProKeys:
                        var proKeysParams = Instruments.DeserializeProKeysParameters(stream, version);
                        return new ProKeysEngineParameters(proKeysParams, baseParams);
                    case GameMode.Vocals:
                        var vocalsParams = Instruments.DeserializeVocalsParameters(stream, version);
                        return new VocalsEngineParameters(vocalsParams, baseParams);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
                }
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
                var baseStats = new SerializedBaseStats();

                baseStats.CommittedScore = stream.Read<int>(Endianness.Little);
                baseStats.PendingScore = stream.Read<int>(Endianness.Little);
                baseStats.NoteScore = stream.Read<int>(Endianness.Little);
                baseStats.SustainScore = stream.Read<int>(Endianness.Little);
                baseStats.MultiplierScore = stream.Read<int>(Endianness.Little);
                baseStats.Combo = stream.Read<int>(Endianness.Little);
                baseStats.MaxCombo = stream.Read<int>(Endianness.Little);
                baseStats.ScoreMultiplier = stream.Read<int>(Endianness.Little);
                baseStats.NotesHit = stream.Read<int>(Endianness.Little);
                baseStats.TotalNotes = stream.Read<int>(Endianness.Little);
                baseStats.StarPowerTickAmount = stream.Read<uint>(Endianness.Little);
                baseStats.TotalStarPowerTicks = stream.Read<uint>(Endianness.Little);
                baseStats.TotalStarPowerBarsFilled = stream.Read<double>(Endianness.Little);
                baseStats.StarPowerActivationCount = stream.Read<int>(Endianness.Little);
                baseStats.TimeInStarPower = stream.Read<double>(Endianness.Little);
                baseStats.StarPowerWhammyTicks = stream.Read<uint>(Endianness.Little);
                baseStats.IsStarPowerActive = stream.ReadBoolean();
                baseStats.StarPowerPhrasesHit = stream.Read<int>(Endianness.Little);
                baseStats.TotalStarPowerPhrases = stream.Read<int>(Endianness.Little);
                baseStats.SoloBonuses = stream.Read<int>(Endianness.Little);
                baseStats.StarPowerScore = stream.Read<int>(Endianness.Little);

                switch (gameMode)
                {
                    case GameMode.FiveFretGuitar:
                    case GameMode.SixFretGuitar:
                        var guitarStats = Instruments.DeserializeGuitarStats(stream, version);
                        return new GuitarStats(guitarStats, baseStats);
                    case GameMode.FourLaneDrums:
                    case GameMode.FiveLaneDrums:
                        var drumsStats = Instruments.DeserializeDrumsStats(stream, version);
                        return new DrumsStats(drumsStats, baseStats);
                    case GameMode.ProGuitar:
                        throw new NotImplementedException("Pro Guitar Replays are not implemented yet!");
                    case GameMode.ProKeys:
                        var proKeysStats = Instruments.DeserializeProKeysStats(stream, version);
                        return new ProKeysStats(proKeysStats, baseStats);
                    case GameMode.Vocals:
                        var vocalsStats = Instruments.DeserializeVocalsStats(stream, version);
                        return new VocalsStats(vocalsStats, baseStats);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
                }
            }

            #endregion
        }
    }
}