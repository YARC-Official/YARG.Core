using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseStats : IBinarySerializable
    {
        /// <summary>
        /// Finalized score (e.g from notes hit and sustains)
        /// </summary>
        public int CommittedScore;

        /// <summary>
        /// Score that is currently pending addition (e.g. from active sustains).
        /// These points are recalculated every update, and only get added once their
        /// final condition has been met.
        /// </summary>
        /// <remarks>
        /// These points are still earned, but their total value is not final yet.
        /// Adding them immediately would result in problems such as precision errors.
        /// </remarks>
        public int PendingScore;

        /// <summary>
        /// Total score across all score values.
        /// </summary>
        /// <remarks>
        /// Calculated from <see cref="CommittedScore"/>, <see cref="PendingScore"/>, and <see cref="SoloBonuses"/>.
        /// </remarks>
        public int TotalScore => CommittedScore + PendingScore + SoloBonuses;

        /// <summary>
        /// The score used to calculate star progress.
        /// </summary>
        /// <remarks>
        /// Calculated from <see cref="CommittedScore"/> and <see cref="PendingScore"/>.
        /// <see cref="SoloBonuses"/> is not included in star progress.
        /// </remarks>
        public int StarScore => CommittedScore + PendingScore;

        /// <summary>
        /// The player's current combo (such as 500 note streak)
        /// </summary>
        public int Combo;

        /// <summary>
        /// The player's highest combo achieved.
        /// </summary>
        public int MaxCombo;

        /// <summary>
        /// The player's current score multiplier (e.g 2x, 3x)
        /// </summary>
        public int ScoreMultiplier;

        /// <summary>
        /// Number of notes which have been hit.
        /// </summary>
        public int NotesHit;

        /// <summary>
        /// Number of notes in the chart. This value should never be modified.
        /// </summary>
        public int TotalNotes;

        /// <summary>
        /// Number of notes which have been missed.
        /// </summary>
        /// <remarks>Value is calculated from <see cref="TotalNotes"/> - <see cref="NotesHit"/>.</remarks>
        public int NotesMissed => TotalNotes - NotesHit;

        /// <summary>
        /// The percent of notes hit.
        /// </summary>
        public virtual float Percent => TotalNotes == 0 ? 1f : (float) NotesHit / TotalNotes;

        /// <summary>
        /// Amount of Star Power/Overdrive the player currently has.
        /// </summary>
        public double StarPowerBarAmount;

        public uint StarPowerTickAmount;

        public uint TotalStarPowerTicks;

        public double TimeInStarPower;

        /// <summary>
        /// True if the player currently has Star Power/Overdrive active.
        /// </summary>
        public bool IsStarPowerActive;

        /// <summary>
        /// Whether or not Star Power/Overdrive can be activated.
        /// </summary>
        //public bool CanStarPowerActivate => StarPowerBarAmount >= 0.5 && !IsStarPowerActive;

        /// <summary>
        /// Number of Star Power phrases which have been hit.
        /// </summary>
        public int StarPowerPhrasesHit;

        /// <summary>
        /// Number of Star Power phrases in the chart. This value should never be modified.
        /// </summary>
        public int TotalStarPowerPhrases;

        /// <summary>
        /// Number of Star Power phrases which have been missed.
        /// </summary>
        /// <remarks>Value is calculated from <see cref="TotalStarPowerPhrases"/> - <see cref="StarPowerPhrasesHit"/>.</remarks>
        public int StarPowerPhrasesMissed => TotalStarPowerPhrases - StarPowerPhrasesHit;

        /// <summary>
        /// Amount of points earned from solo bonuses.
        /// </summary>
        public int SoloBonuses;

        /// <summary>
        /// The number of stars the player has achieved, along with the progress to the next star.
        /// </summary>
        public float Stars;

        protected BaseStats()
        {
        }

        protected BaseStats(BaseStats stats)
        {
            CommittedScore = stats.CommittedScore;
            PendingScore = stats.PendingScore;
            Combo = stats.Combo;
            MaxCombo = stats.MaxCombo;
            ScoreMultiplier = stats.ScoreMultiplier;
            NotesHit = stats.NotesHit;
            TotalNotes = stats.TotalNotes;

            StarPowerBarAmount = stats.StarPowerBarAmount;
            StarPowerTickAmount = stats.StarPowerTickAmount;
            TotalStarPowerTicks = stats.TotalStarPowerTicks;
            TimeInStarPower = stats.TimeInStarPower;
            IsStarPowerActive = stats.IsStarPowerActive;

            StarPowerPhrasesHit = stats.StarPowerPhrasesHit;
            TotalStarPowerPhrases = stats.TotalStarPowerPhrases;

            SoloBonuses = stats.SoloBonuses;
            Stars = stats.Stars;
        }

        public virtual void Reset()
        {
            CommittedScore = 0;
            PendingScore = 0;
            Combo = 0;
            MaxCombo = 0;
            ScoreMultiplier = 1;
            NotesHit = 0;
            // Don't reset TotalNotes
            // TotalNotes = 0;

            StarPowerBarAmount = 0;
            StarPowerTickAmount = 0;
            TotalStarPowerTicks = 0;
            TimeInStarPower = 0;
            IsStarPowerActive = false;

            StarPowerPhrasesHit = 0;
            // TotalStarPowerPhrases = 0;

            SoloBonuses = 0;
            Stars = 0;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(CommittedScore);
            writer.Write(PendingScore);

            writer.Write(Combo);
            writer.Write(MaxCombo);
            writer.Write(ScoreMultiplier);

            writer.Write(NotesHit);
            writer.Write(TotalNotes);

            writer.Write(StarPowerBarAmount);
            writer.Write(StarPowerTickAmount);
            writer.Write(TotalStarPowerTicks);
            writer.Write(TimeInStarPower);
            writer.Write(IsStarPowerActive);

            writer.Write(StarPowerPhrasesHit);
            writer.Write(TotalStarPowerPhrases);

            writer.Write(SoloBonuses);

            // Deliberately not written so that stars can be re-calculated with different thresholds
            // writer.Write(Stars);
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            CommittedScore = reader.ReadInt32();
            PendingScore = reader.ReadInt32();

            Combo = reader.ReadInt32();
            MaxCombo = reader.ReadInt32();
            ScoreMultiplier = reader.ReadInt32();

            NotesHit = reader.ReadInt32();
            TotalNotes = reader.ReadInt32();

            StarPowerBarAmount = reader.ReadDouble();
            StarPowerTickAmount = reader.ReadUInt32();
            TotalStarPowerTicks = reader.ReadUInt32();
            TimeInStarPower = reader.ReadDouble();
            IsStarPowerActive = reader.ReadBoolean();

            StarPowerPhrasesHit = reader.ReadInt32();
            TotalStarPowerPhrases = reader.ReadInt32();

            SoloBonuses = reader.ReadInt32();

            // Deliberately not read so that stars can be re-calculated if thresholds change
            // Stars = reader.ReadInt32();
        }
    }
}