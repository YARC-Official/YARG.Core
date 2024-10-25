using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Replays;

namespace YARG.Core.Engine.Drums
{
    public class DrumsStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        /// <summary>
        /// Number of ghosts the player hit with correct dynamics.
        /// </summary>
        public int GhostsHit;

        /// <summary>
        /// Total number of ghost notes in the chart.
        /// </summary>
        public int TotalGhosts;

        /// <summary>
        /// Number of accents the player hit with correct dynamics.
        /// </summary>
        public int AccentsHit;

        /// <summary>
        /// Total number of accent notes in the chart.
        /// </summary>
        public int TotalAccents;

        /// <summary>
        /// Amount of points earned from hitting notes with correct dynamics.
        /// </summary>
        public int DynamicsBonus;

        public DrumsStats()
        {
        }

        public DrumsStats(DrumsStats stats) : base(stats)
        {
            Overhits = stats.Overhits;
            GhostsHit = stats.GhostsHit;
            TotalGhosts = stats.TotalGhosts;
            AccentsHit = stats.AccentsHit;
            TotalAccents = stats.TotalAccents;
            DynamicsBonus = stats.DynamicsBonus;
        }

        public DrumsStats(UnmanagedMemoryStream stream, int version)
            : base(stream, version)
        {
            Overhits = stream.Read<int>(Endianness.Little);
            GhostsHit = stream.Read<int>(Endianness.Little);
            TotalGhosts = stream.Read<int>(Endianness.Little);
            AccentsHit = stream.Read<int>(Endianness.Little);
            TotalAccents = stream.Read<int>(Endianness.Little);
            DynamicsBonus = stream.Read<int>(Endianness.Little);
        }

        public override void Reset()
        {
            base.Reset();
            Overhits = 0;
            GhostsHit = 0;
            // Don't reset TotalGhosts
            // TotalGhosts = 0;

            AccentsHit = 0;
            // Don't reset TotalAccents
            // TotalAccents = 0;

            DynamicsBonus = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Overhits);
            writer.Write(GhostsHit);
            writer.Write(TotalGhosts);
            writer.Write(AccentsHit);
            writer.Write(TotalAccents);
            writer.Write(DynamicsBonus);
        }

        public override ReplayStats ConstructReplayStats(string name)
        {
            return new DrumsReplayStats(name, this);
        }
    }
}