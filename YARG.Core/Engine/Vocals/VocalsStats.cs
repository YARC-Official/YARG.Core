using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Replays;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsStats : BaseStats
    {
        /// <summary>
        /// The amount of note ticks that was hit by the vocalist.
        /// </summary>
        public uint TicksHit;

        /// <summary>
        /// The amount of note ticks that were missed by the vocalist.
        /// </summary>
        public uint TicksMissed;

        /// <summary>
        /// The total amount of note ticks.
        /// </summary>
        public uint TotalTicks => TicksHit + TicksMissed;

        /// <summary>
        /// The amount of vocal phrases that were hit by the vocalist.
        /// Phrases can be hit partially, so this is a double.
        /// </summary>
        public double PhrasesHit;

        /// <summary>
        /// The amount of note ticks that were hit by the vocalist while in Star Power.
        /// Does not include ticks hit after the minimum perfect percent has been reached.
        /// </summary>
        public uint StarPowerBonusTicks;

        /// <summary>
        /// The number of percussion notes that were hit by the vocalist.
        /// </summary>
        public int PercussionNotesHit;

        /// <summary>
        /// The total number of percussion notes in the chart. Should not be modified.
        /// </summary>
        public int TotalPercussionNotes;

        public bool HasCarryNote;

        public override float Percent => TotalNotes == 0 ? 1f : (float) PhrasesHit / TotalNotes;

        public override int BandComboUnits => 10;

        public VocalsStats()
        {
        }

        public VocalsStats(VocalsStats stats) : base(stats)
        {
            TicksHit = stats.TicksHit;
            TicksMissed = stats.TicksMissed;
            HasCarryNote = stats.HasCarryNote;
            PhrasesHit = stats.PhrasesHit;
            StarPowerBonusTicks = stats.StarPowerBonusTicks;
            PercussionNotesHit = stats.PercussionNotesHit;
            TotalPercussionNotes = stats.TotalPercussionNotes;
        }

        public VocalsStats(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            TicksHit = stream.Read<uint>(Endianness.Little);
            TicksMissed = stream.Read<uint>(Endianness.Little);
            HasCarryNote = stream.ReadBoolean();
            if (version >= 16)
            {
                PhrasesHit = stream.Read<double>(Endianness.Little);
                StarPowerBonusTicks = stream.Read<uint>(Endianness.Little);
                PercussionNotesHit = stream.Read<int>(Endianness.Little);
                TotalPercussionNotes = stream.Read<int>(Endianness.Little);
            }
        }

        public override void Reset()
        {
            base.Reset();
            TicksHit = 0;
            TicksMissed = 0;
            HasCarryNote = false;
            StarPowerBonusTicks = 0;
            PercussionNotesHit = 0;
            PhrasesHit = 0;
        }

        public override IReadOnlyList<double> GetOffsetSamples()
        {
            return Array.Empty<double>();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(TicksHit);
            writer.Write(TicksMissed);
            writer.Write(HasCarryNote);
            writer.Write(PhrasesHit);
            writer.Write(StarPowerBonusTicks);
            writer.Write(PercussionNotesHit);
            writer.Write(TotalPercussionNotes);
        }

        public override ReplayStats ConstructReplayStats(string name, bool isReplayPlayer)
        {
            return new VocalsReplayStats(name, isReplayPlayer, this);
        }
    }
}