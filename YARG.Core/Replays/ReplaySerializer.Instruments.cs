using System.IO;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;
using YARG.Core.Replays.Serialization;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public static partial class ReplaySerializer
    {
        private static class Instruments
        {
            #region Guitar

            #region Parameters

            public static void SerializeGuitarParameters(BinaryWriter writer, GuitarEngineParameters parameters)
            {
                writer.Write(parameters.HopoLeniency);
                writer.Write(parameters.StrumLeniency);
                writer.Write(parameters.StrumLeniencySmall);
                writer.Write(parameters.InfiniteFrontEnd);
                writer.Write(parameters.AntiGhosting);
            }

            public static SerializedGuitarEngineParameters DeserializeGuitarParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedGuitarEngineParameters();

                parameters.HopoLeniency = stream.Read<double>(Endianness.Little);
                parameters.StrumLeniency = stream.Read<double>(Endianness.Little);
                parameters.StrumLeniencySmall = stream.Read<double>(Endianness.Little);
                parameters.InfiniteFrontEnd = stream.ReadBoolean();
                parameters.AntiGhosting = stream.ReadBoolean();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeGuitarStats(BinaryWriter writer, GuitarStats stats)
            {
                writer.Write(stats.Overstrums);
                writer.Write(stats.HoposStrummed);
                writer.Write(stats.GhostInputs);
            }

            public static SerializedGuitarStats DeserializeGuitarStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedGuitarStats();

                stats.Overstrums = stream.Read<int>(Endianness.Little);
                stats.HoposStrummed = stream.Read<int>(Endianness.Little);
                stats.GhostInputs = stream.Read<int>(Endianness.Little);

                return stats;
            }

            #endregion

            #endregion

            #region Drums

            #region Parameters

            public static void SerializeDrumsParameters(BinaryWriter writer, DrumsEngineParameters parameters)
            {
                writer.Write((byte) parameters.Mode);
                writer.Write(parameters.VelocityThreshold);
                writer.Write(parameters.SituationalVelocityWindow);
            }

            public static SerializedDrumsEngineParameters DeserializeDrumsParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedDrumsEngineParameters();

                parameters.Mode = (DrumsEngineParameters.DrumMode) stream.ReadByte();
                parameters.VelocityThreshold = stream.Read<float>(Endianness.Little);
                parameters.SituationalVelocityWindow = stream.Read<float>(Endianness.Little);

                return parameters;
            }

            #endregion'

            #region Stats

            public static void SerializeDrumsStats(BinaryWriter writer, DrumsStats stats)
            {
                writer.Write(stats.Overhits);
            }

            public static SerializedDrumsStats DeserializeDrumsStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedDrumsStats();

                stats.Overhits = stream.Read<int>(Endianness.Little);

                return stats;
            }

            #endregion

            #endregion

            #region Pro Keys

            #region Parameters

            public static void SerializeProKeysParameters(BinaryWriter writer, ProKeysEngineParameters parameters)
            {
                writer.Write(parameters.ChordStaggerWindow);
                writer.Write(parameters.FatFingerWindow);
            }

            public static SerializedProKeysEngineParameters DeserializeProKeysParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedProKeysEngineParameters();

                parameters.ChordStaggerWindow = stream.Read<double>(Endianness.Little);
                parameters.FatFingerWindow = stream.Read<double>(Endianness.Little);

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeProKeysStats(BinaryWriter writer, ProKeysStats stats)
            {
                writer.Write(stats.Overhits);
            }

            public static SerializedProKeysStats DeserializeProKeysStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedProKeysStats();

                stats.Overhits = stream.Read<int>(Endianness.Little);

                return stats;
            }

            #endregion

            #endregion

            #region Vocals

            #region Parameters

            public static void SerializeVocalsParameters(BinaryWriter writer, VocalsEngineParameters parameters)
            {
                writer.Write(parameters.PitchWindow);
                writer.Write(parameters.PitchWindowPerfect);
                writer.Write(parameters.PhraseHitPercent);
                writer.Write(parameters.ApproximateVocalFps);
                writer.Write(parameters.SingToActivateStarPower);
                writer.Write(parameters.PointsPerPhrase);
            }

            public static SerializedVocalsEngineParameters DeserializeVocalsParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedVocalsEngineParameters();

                parameters.PitchWindow = stream.Read<float>(Endianness.Little);
                parameters.PitchWindowPerfect = stream.Read<float>(Endianness.Little);
                parameters.PhraseHitPercent = stream.Read<double>(Endianness.Little);
                parameters.ApproximateVocalFps = stream.Read<double>(Endianness.Little);
                parameters.SingToActivateStarPower = stream.ReadBoolean();
                parameters.PointsPerPhrase = stream.Read<int>(Endianness.Little);

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeVocalsStats(BinaryWriter writer, VocalsStats stats)
            {
                writer.Write(stats.TicksHit);
                writer.Write(stats.TicksMissed);
            }

            public static SerializedVocalsStats DeserializeVocalsStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedVocalsStats();

                stats.TicksHit = stream.Read<uint>(Endianness.Little);
                stats.TicksMissed = stream.Read<uint>(Endianness.Little);

                return stats;
            }

            #endregion

            #endregion
        }
    }
}