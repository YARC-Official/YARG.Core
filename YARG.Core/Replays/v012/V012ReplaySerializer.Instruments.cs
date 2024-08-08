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
                writer.Write((byte) parameters.Mode);
            }

            public static DrumsEngineParameters DeserializeDrumsParameters(ref SpanBinaryReader reader, int version = 0)
            {
                var parameters = new DrumsEngineParameters();

                parameters.Mode = (DrumsEngineParameters.DrumMode) reader.ReadByte();

                return parameters;
            }

            #endregion

            #region Stats

            public static void SerializeDrumsStats(BinaryWriter writer, DrumsStats stats)
            {
                //writer.Write(stats.Overhits);
            }

            public static DrumsStats DeserializeDrumsStats(ref SpanBinaryReader reader, int version = 0)
            {
                var stats = new DrumsStats();

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

            public static VocalsEngineParameters DeserializeVocalsParameters(ref SpanBinaryReader reader, int version = 0)
            {
                var parameters = new VocalsEngineParameters();

                parameters.PhraseHitPercent = reader.ReadDouble();
                parameters.ApproximateVocalFps = reader.ReadDouble();
                parameters.SingToActivateStarPower = reader.ReadBoolean();

                var positionUpToPhrasePts = reader.Position;

                // This variable was never accounted for in v0.12 versioning which means it's a bit tricky to determine
                // if it was written.
                var ptsPerPhrase = reader.ReadInt32();

                var positionAfterPhrasePts = reader.Position;

                if (ptsPerPhrase != 400 && ptsPerPhrase != 800 && ptsPerPhrase != 1600 && ptsPerPhrase != 2000)
                {
                    // If the int32 read isn't any of these values, PointsPerPhrase was never written
                    reader.Seek(positionUpToPhrasePts);
                    parameters.PointsPerPhrase = 2000;
                }
                else
                {
                    // If it was one of these values, it could still not be written (it could be the CommittedScore)
                    // We need to check the next few vars

                    var committedScore = reader.ReadInt32();
                    var pendingScore = reader.ReadInt32();
                    var combo = reader.ReadInt32();
                    var maxCombo = reader.ReadInt32();
                    var scoreMultiplier = reader.ReadInt32();
                    var notesHit = reader.ReadInt32();

                    if (pendingScore != 0 || maxCombo > notesHit)
                    {
                        // pendingScore should always be 0 in vocals
                        // maxCombo can never be bigger than the number of notes hit

                        // Walk back the position because PointsPerPhrase was never written
                        reader.Seek(positionUpToPhrasePts);
                    }
                    else
                    {
                        // If all these checks pass, we can assume PointsPerPhrase was written
                        // Seek back to the position after reading PointsPerPhrase
                        reader.Seek(positionAfterPhrasePts);
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