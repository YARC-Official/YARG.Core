using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.IO;

namespace YARG.Core.Replays
{
    public abstract class ReplayStats
    {
        public readonly string PlayerName;
        public readonly int    Score;
        public readonly float  Stars;
        public readonly int    TotalOverdrivePhrases;
        public readonly int    NumOverdrivePhrasesHit;
        public readonly int    NumOverdriveActivations;
        public readonly float  AverageMultiplier;
        public readonly int    NumPauses;
        public readonly bool   IsReplayPlayer;

        protected ReplayStats(string name, BaseStats stats, bool isReplayPlayer)
        {
            PlayerName = name;
            Score = stats.TotalScore;
            Stars = stats.Stars;
            TotalOverdrivePhrases = stats.TotalStarPowerPhrases;
            NumOverdrivePhrasesHit = TotalOverdrivePhrases - stats.StarPowerPhrasesMissed;
            NumOverdriveActivations = stats.StarPowerActivationCount;
            AverageMultiplier = 0;
            NumPauses = 0;
            IsReplayPlayer = isReplayPlayer;
        }

        protected ReplayStats(ref FixedArrayStream stream, int version)
        {
            PlayerName = stream.ReadString();
            Score = stream.Read<int>(Endianness.Little);
            Stars = stream.Read<float>(Endianness.Little);
            TotalOverdrivePhrases = stream.Read<int>(Endianness.Little);
            NumOverdrivePhrasesHit = stream.Read<int>(Endianness.Little);
            NumOverdriveActivations = stream.Read<int>(Endianness.Little);
            AverageMultiplier = stream.Read<float>(Endianness.Little);
            NumPauses = stream.Read<int>(Endianness.Little);

            if (version >= 14)
            {
                IsReplayPlayer = stream.ReadBoolean();
            }
            else
            {
                // Possibly a lie, but we were already lying by omission
                IsReplayPlayer = false;
            }
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerName);
            writer.Write(Score);
            writer.Write(Stars);
            writer.Write(TotalOverdrivePhrases);
            writer.Write(NumOverdrivePhrasesHit);
            writer.Write(NumOverdriveActivations);
            writer.Write(AverageMultiplier);
            writer.Write(NumPauses);
            writer.Write(IsReplayPlayer);
        }
    }
}
