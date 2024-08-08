using System.IO;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public static partial class V012ReplaySerializer
    {
        private static class Instruments
        {
            # region Guitar

            # region Parameters

            public static void SerializeGuitarParameters(BinaryWriter writer, GuitarEngineParameters parameters)
            {
                writer.Write(parameters.HopoLeniency);
                writer.Write(parameters.StrumLeniency);
                writer.Write(parameters.StrumLeniencySmall);
                writer.Write(parameters.StarPowerWhammyBuffer);
                writer.Write(parameters.InfiniteFrontEnd);
                writer.Write(parameters.AntiGhosting);
            }

            public static GuitarEngineParameters DeserializeGuitarParameters(ref SpanBinaryReader reader, int version = 0)
            {
                var parameters = new GuitarEngineParameters();

                parameters.HopoLeniency = reader.ReadDouble();
                parameters.StrumLeniency = reader.ReadDouble();
                parameters.StrumLeniencySmall = reader.ReadDouble();
                parameters.StarPowerWhammyBuffer = reader.ReadDouble();
                parameters.InfiniteFrontEnd = reader.ReadBoolean();
                parameters.AntiGhosting = reader.ReadBoolean();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeGuitarStats(BinaryWriter writer, GuitarStats stats)
            {
                writer.Write(stats.Overstrums);
                writer.Write(stats.HoposStrummed);
                writer.Write(stats.GhostInputs);
                writer.Write(0.0); // StarPowerWhammyGain - No longer exists
            }

            public static GuitarStats DeserializeGuitarStats(ref SpanBinaryReader reader, int version = 0)
            {
                var stats = new GuitarStats();

                stats.Overstrums = reader.ReadInt32();
                stats.HoposStrummed = reader.ReadInt32();
                stats.GhostInputs = reader.ReadInt32();
                reader.ReadDouble();

                return stats;
            }

            #endregion

            #endregion

            #region Drums

            #region Parameters

            public static void SerializeDrumsParameters(BinaryWriter writer, DrumsEngineParameters parameters)
            {
                writer.Write((int) parameters.Mode);
            }

            public static DrumsEngineParameters DeserializeDrumsParameters(ref SpanBinaryReader reader, int version = 0)
            {
                var parameters = new DrumsEngineParameters();

                parameters.Mode = (DrumsEngineParameters.DrumMode) reader.ReadInt32();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeDrumsStats(BinaryWriter writer, DrumsStats stats)
            {
                writer.Write(stats.Overhits);
            }

            public static DrumsStats DeserializeDrumsStats(ref SpanBinaryReader reader, int version = 0)
            {
                var stats = new DrumsStats();

                stats.Overhits = reader.ReadInt32();

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

            public static VocalsEngineParameters DeserializeVocalsParameters(ref SpanBinaryReader reader, int version = 0)
            {
                var parameters = new VocalsEngineParameters();

                parameters.PitchWindow = reader.ReadSingle();
                parameters.PitchWindowPerfect = reader.ReadSingle();
                parameters.PhraseHitPercent = reader.ReadDouble();
                parameters.ApproximateVocalFps = reader.ReadDouble();
                parameters.SingToActivateStarPower = reader.ReadBoolean();
                parameters.PointsPerPhrase = reader.ReadInt32();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeVocalsStats(BinaryWriter writer, VocalsStats stats)
            {
                writer.Write(stats.TicksHit);
                writer.Write(stats.TicksMissed);
            }

            public static VocalsStats DeserializeVocalsStats(ref SpanBinaryReader reader, int version = 0)
            {
                var stats = new VocalsStats();

                stats.TicksHit = reader.ReadUInt32();
                stats.TicksMissed = reader.ReadUInt32();

                return stats;
            }

            #endregion

            #endregion
        }
    }
}