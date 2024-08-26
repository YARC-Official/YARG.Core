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

            public static SerializedGuitarEngineParameters DeserializeGuitarParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedGuitarEngineParameters();

                parameters.HopoLeniency = stream.Read<double>(Endianness.Little);
                parameters.StrumLeniency = stream.Read<double>(Endianness.Little);
                parameters.StrumLeniencySmall = stream.Read<double>(Endianness.Little);
                parameters.StarPowerWhammyBuffer = stream.Read<double>(Endianness.Little);
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
                writer.Write(0.0); // StarPowerWhammyGain - No longer exists
            }

            public static SerializedGuitarStats DeserializeGuitarStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedGuitarStats();

                stats.Overstrums = stream.Read<int>(Endianness.Little);
                stats.HoposStrummed = stream.Read<int>(Endianness.Little);
                stats.GhostInputs = stream.Read<int>(Endianness.Little);
                var spWhammyGain = stream.Read<double>(Endianness.Little);

                return stats;
            }

            #endregion

            #endregion

            #region Drums

            #region Parameters

            public static void SerializeDrumsParameters(BinaryWriter writer, DrumsEngineParameters parameters)
            {
                writer.Write((byte) parameters.Mode);
            }

            public static SerializedDrumsEngineParameters DeserializeDrumsParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedDrumsEngineParameters();

                parameters.Mode = (DrumsEngineParameters.DrumMode) stream.ReadByte();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeDrumsStats(BinaryWriter writer, DrumsStats stats)
            {
                //writer.Write(stats.Overhits);
            }

            public static SerializedDrumsStats DeserializeDrumsStats(UnmanagedMemoryStream stream, int version = 0)
            {
                var stats = new SerializedDrumsStats();

                // v0.12 never wrote overhits - whoopsie
                //stats.Overhits = reader.ReadInt32();

                return stats;
            }

            #endregion

            #endregion

            #region Vocals

            #region Parameters

            public static void SerializeVocalsParameters(BinaryWriter writer, VocalsEngineParameters parameters)
            {
                writer.Write(parameters.PhraseHitPercent);
                writer.Write(parameters.ApproximateVocalFps);
                writer.Write(parameters.SingToActivateStarPower);
                writer.Write(parameters.PointsPerPhrase);
            }

            public static SerializedVocalsEngineParameters DeserializeVocalsParameters(UnmanagedMemoryStream stream, int version = 0)
            {
                var parameters = new SerializedVocalsEngineParameters();

                parameters.PhraseHitPercent = stream.Read<double>(Endianness.Little);
                parameters.ApproximateVocalFps = stream.Read<double>(Endianness.Little);
                parameters.SingToActivateStarPower = stream.ReadBoolean();

                var positionUpToPhrasePts = stream.Position;

                // This variable was never accounted for in v0.12 versioning which means it's a bit tricky to determine
                // if it was written.
                var ptsPerPhrase = stream.Read<int>(Endianness.Little);

                var positionAfterPhrasePts = stream.Position;

                if (ptsPerPhrase != 400 && ptsPerPhrase != 800 && ptsPerPhrase != 1600 && ptsPerPhrase != 2000)
                {
                    // If the int32 read isn't any of these values, PointsPerPhrase was never written
                    stream.Seek(positionUpToPhrasePts, SeekOrigin.Begin);
                    parameters.PointsPerPhrase = 2000;
                }
                else
                {
                    // If it was one of these values, it could still not be written (it could be the CommittedScore)
                    // We need to check the next few vars

                    var committedScore = stream.Read<int>(Endianness.Little);

                    // This variable isn't reliable as it wasn't versioned either!
                    var pendingScore = stream.Read<int>(Endianness.Little);

                    var combo = stream.Read<int>(Endianness.Little);
                    var maxCombo = stream.Read<int>(Endianness.Little);
                    var scoreMultiplier = stream.Read<int>(Endianness.Little);
                    var notesHit = stream.Read<int>(Endianness.Little);
                    var notesMissed = stream.Read<int>(Endianness.Little);

                    var unusedSpAmount = stream.Read<int>(Endianness.Little);
                    var unusedSpBaseAmount = stream.Read<int>(Endianness.Little);

                    var isSpActiveByte = (byte) stream.ReadByte();
                    var isSpActive = isSpActiveByte != 0;

                    if (maxCombo > notesHit || isSpActiveByte > 1 ||
                        (!isSpActive && scoreMultiplier > combo - 1))
                    {
                        // pendingScore should always be 0 in vocals (if it was written)
                        // maxCombo can never be bigger than the number of notes hit
                        // The byte for isSpActive can only be 0 or 1
                        // If SP isn't active, the score multiplier can't be higher than the combo - 1

                        // Walk back the position because PointsPerPhrase was never written
                        stream.Seek(positionUpToPhrasePts, SeekOrigin.Begin);
                    }
                    else
                    {
                        // If all these checks pass, we can assume PointsPerPhrase was written
                        // Seek back to the position after reading PointsPerPhrase
                        stream.Seek(positionAfterPhrasePts, SeekOrigin.Begin);
                        parameters.PointsPerPhrase = ptsPerPhrase;
                    }
                }

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