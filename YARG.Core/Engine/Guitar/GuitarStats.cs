using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Replays;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarStats : BaseStats
    {
        /// <summary>
        /// Number of overstrums which have occurred.
        /// </summary>
        public int Overstrums;

        /// <summary>
        /// Number of hammer-ons/pull-offs which have been strummed.
        /// </summary>
        public int HoposStrummed;

        /// <summary>
        /// Number of ghost inputs the player has made.
        /// </summary>
        public int GhostInputs;

        public GuitarStats()
        {
        }

        public GuitarStats(GuitarStats stats) : base(stats)
        {
            Overstrums = stats.Overstrums;
            HoposStrummed = stats.HoposStrummed;
            GhostInputs = stats.GhostInputs;
            SustainScore = stats.SustainScore;
        }

        public GuitarStats(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            Overstrums = stream.Read<int>(Endianness.Little);
            HoposStrummed = stream.Read<int>(Endianness.Little);
            GhostInputs = stream.Read<int>(Endianness.Little);
            SustainScore = stream.Read<int>(Endianness.Little);
        }

        public override void Reset()
        {
            base.Reset();
            Overstrums = 0;
            HoposStrummed = 0;
            GhostInputs = 0;
            SustainScore = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Overstrums);
            writer.Write(HoposStrummed);
            writer.Write(GhostInputs);
            writer.Write(SustainScore);
        }

        public override ReplayStats ConstructReplayStats(string name)
        {
            return new GuitarReplayStats(name, this);
        }
    }
}