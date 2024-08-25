using System;
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
    public static partial class V012ReplaySerializer
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
                var metadata = new ReplayMetadata();

                metadata.SongName = stream.ReadString();
                metadata.ArtistName = stream.ReadString();
                metadata.CharterName = stream.ReadString();
                metadata.BandScore = stream.Read<int>(Endianness.Little);
                metadata.BandStars = (StarAmount) stream.ReadByte();
                metadata.ReplayLength = stream.Read<double>(Endianness.Little);
                metadata.Date = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                metadata.SongChecksum = HashWrapper.Deserialize(stream);

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
                var presetContainer = new ReplayPresetContainer();
                presetContainer.Deserialize(stream, version);
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
                var frame = new ReplayFrame();
                frame.PlayerInfo = DeserializePlayerInfo(stream, version);
                frame.EngineParameters =
                    DeserializeEngineParameters(stream, frame.PlayerInfo.Profile.GameMode, version);
                frame.Stats = DeserializeStats(stream, frame.PlayerInfo.Profile.GameMode, version);

                frame.InputCount = stream.Read<int>(Endianness.Little);
                frame.Inputs = new GameInput[frame.InputCount];
                for (int i = 0; i < frame.InputCount; i++)
                {
                    var time = stream.Read<double>(Endianness.Little);
                    var action = stream.Read<int>(Endianness.Little);
                    var value = stream.Read<int>(Endianness.Little);

                    frame.Inputs[i] = new GameInput(time, action, value);
                }

                // Event logger was removed in version 6+
                if (version <= 5)
                {
                    var eventLogger = new EngineEventLogger();

                    eventLogger.Deserialize(stream, version);
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

                //writer.Write(profile.Id);
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

                //profile.Id = stream.ReadGuid();

                profile.Name = stream.ReadString();

                profile.EnginePreset = stream.ReadGuid();

                profile.ThemePreset = stream.ReadGuid();
                profile.ColorProfile = stream.ReadGuid();
                profile.CameraPreset = stream.ReadGuid();

                profile.CurrentInstrument = (Instrument) stream.ReadByte();
                profile.CurrentDifficulty = (Difficulty) stream.ReadByte();
                profile.CurrentModifiers = (Modifier) stream.Read<long>(Endianness.Little);
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
                writer.Write(engineParameters.HitWindow.IsDynamic);
                writer.Write(engineParameters.HitWindow.FrontToBackRatio);

                writer.Write(engineParameters.MaxMultiplier);
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
                    case GameMode.Vocals:
                        Instruments.SerializeVocalsParameters(writer, (engineParameters as VocalsEngineParameters)!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), "Unsupported game mode: " + gameMode);
                }
            }

            public static BaseEngineParameters DeserializeEngineParameters(UnmanagedMemoryStream stream,
                GameMode gameMode,
                int version = 0)
            {
                var maxWindow = stream.Read<double>(Endianness.Little);
                var minWindow = stream.Read<double>(Endianness.Little);
                var isDynamic = stream.ReadBoolean();
                var frontToBackRatio = stream.Read<double>(Endianness.Little);

                var positionUpToMultiplier = stream.Position;

                // This variable was not accounted for in versioning
                int maxMultiplier = stream.Read<int>(Endianness.Little);

                var multiplierThresholdLength = stream.Read<int>(Endianness.Little);

                // This was always 6 in v0.12 so if its not then maxMultiplier was never written
                if (multiplierThresholdLength != 6)
                {
                    stream.Seek(positionUpToMultiplier, SeekOrigin.Begin);

                    // Bass multiplier was 4 before maxMultiplier was added
                    maxMultiplier = 4;

                    multiplierThresholdLength = stream.Read<int>(Endianness.Little);
                }

                float[] starMultiplierThresholds = new float[multiplierThresholdLength];
                for (int i = 0; i < starMultiplierThresholds.Length; i++)
                {
                    starMultiplierThresholds[i] = stream.Read<float>(Endianness.Little);
                }

                double songSpeed = 1;
                if (version >= 5)
                {
                    songSpeed = stream.Read<double>(Endianness.Little);
                }

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
                    case GameMode.Vocals:
                        engineParameters = Instruments.DeserializeVocalsParameters(stream, version);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), "Unsupported game mode: " + gameMode);
                }

                var hitWindow = new HitWindowSettings(maxWindow, minWindow, frontToBackRatio, isDynamic, 0, 0, 0);

                engineParameters.HitWindow = hitWindow;
                engineParameters.MaxMultiplier = maxMultiplier;
                engineParameters.StarMultiplierThresholds = starMultiplierThresholds;
                engineParameters.SongSpeed = songSpeed;

                return engineParameters;
            }

            #endregion

            #region Stats

            public static void SerializeStats(BinaryWriter writer, BaseStats stats, GameMode gameMode)
            {
                writer.Write(stats.CommittedScore);
                writer.Write(stats.PendingScore);
                writer.Write(stats.Combo);
                writer.Write(stats.MaxCombo);
                writer.Write(stats.ScoreMultiplier);
                writer.Write(stats.NotesHit);
                writer.Write(stats.TotalNotes);
                writer.Write(0.0); // StarPowerAmount - No longer exists
                writer.Write(0.0); // StarPowerBaseAmount - No longer exists
                writer.Write(stats.IsStarPowerActive);
                writer.Write(stats.StarPowerPhrasesHit);
                writer.Write(stats.TotalStarPowerPhrases);
                writer.Write(stats.SoloBonuses);
                writer.Write(stats.Stars);
            }

            public static BaseStats DeserializeStats(UnmanagedMemoryStream stream, GameMode gameMode, int version = 0)
            {
                var committedScore = stream.Read<int>(Endianness.Little);

                var positionUpToPendingScore = stream.Position;

                // Another variable which was not versioned :(
                var pendingScore = stream.Read<int>(Endianness.Little);

                var positionAfterPendingScore = stream.Position;

                var tempCombo = stream.Read<int>(Endianness.Little);
                var tempMaxCombo = stream.Read<int>(Endianness.Little);
                var tempScoreMultiplier = stream.Read<int>(Endianness.Little);
                var tempNotesHit = stream.Read<int>(Endianness.Little);
                var tempTotalNotes = stream.Read<int>(Endianness.Little);
                var tempUnusedStarPowerAmount = stream.Read<double>(Endianness.Little);
                var tempUnusedStarPowerBaseAmount = stream.Read<double>(Endianness.Little);

                var tempIsSpActiveByte = (byte) stream.ReadByte();

                // If any of these pass its really suspicious so pendingScore likely was not written
                if (tempCombo > tempMaxCombo || tempScoreMultiplier > 12 || tempNotesHit < tempMaxCombo
                    || tempIsSpActiveByte > 1
                    || tempUnusedStarPowerAmount is > 0 and < 1.28854456937478154416e-231
                    || tempUnusedStarPowerBaseAmount is > 0 and < 1.28854456937478154416e-231
                    || (gameMode == GameMode.Vocals && tempIsSpActiveByte == 0 && tempCombo <= 3 && tempScoreMultiplier - 1 != tempCombo))
                {
                    stream.Seek(positionUpToPendingScore, SeekOrigin.Begin);
                    pendingScore = 0;
                }
                else
                {
                    stream.Seek(positionAfterPendingScore, SeekOrigin.Begin);
                }

                var combo = stream.Read<int>(Endianness.Little);
                var maxCombo = stream.Read<int>(Endianness.Little);
                var scoreMultiplier = stream.Read<int>(Endianness.Little);
                var notesHit = stream.Read<int>(Endianness.Little);
                var totalNotes = stream.Read<int>(Endianness.Little);

                // NotesMissed was replaced with TotalNotes at some point
                // There's no way of telling besides seeing if they notesHit is bigger than "totalNotes"
                // But in reality this could be true as it was missed notes, and you can miss more than you hit
                if (notesHit > totalNotes)
                {
                    var missedNotes = totalNotes;
                    totalNotes = notesHit + missedNotes;
                }

                var starPowerAmount = stream.Read<double>(Endianness.Little);
                var starPowerBaseAmount = stream.Read<double>(Endianness.Little);
                var isStarPowerActive = stream.ReadBoolean();
                var starPowerPhrasesHit = stream.Read<int>(Endianness.Little);
                var totalStarPowerPhrases = stream.Read<int>(Endianness.Little);

                // Same as above
                if (starPowerPhrasesHit > totalStarPowerPhrases)
                {
                    var spMissed = totalStarPowerPhrases;
                    totalStarPowerPhrases = starPowerPhrasesHit + spMissed;
                }

                var soloBonuses = stream.Read<int>(Endianness.Little);

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
                    case GameMode.Vocals:
                        stats = Instruments.DeserializeVocalsStats(stream, version);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(gameMode), "Unsupported game mode: " + gameMode);
                }

                stats.CommittedScore = committedScore;
                stats.PendingScore = pendingScore;
                stats.Combo = combo;
                stats.MaxCombo = maxCombo;
                stats.ScoreMultiplier = scoreMultiplier;
                stats.NotesHit = notesHit;
                stats.TotalNotes = totalNotes;
                //stats.StarPowerAmount = starPowerAmount;
                //stats.StarPowerBaseAmount = starPowerBaseAmount;
                stats.IsStarPowerActive = isStarPowerActive;
                stats.StarPowerPhrasesHit = starPowerPhrasesHit;
                stats.TotalStarPowerPhrases = totalStarPowerPhrases;
                stats.SoloBonuses = soloBonuses;

                return stats;
            }

            #endregion
        }
    }
}