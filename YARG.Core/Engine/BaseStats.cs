namespace YARG.Core.Engine
{
    public abstract class BaseStats
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
        /// Total score earned from hitting notes.
        /// </summary>
        public int NoteScore;

        /// <summary>
        /// Total score earned from holding sustains.
        /// </summary>
        public int SustainScore;

        /// <summary>
        /// Total score earned from score multipliers.
        /// </summary>
        public int MultiplierScore;

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

        public uint StarPowerTickAmount;

        public uint TotalStarPowerTicks;

        public double TotalStarPowerBarsFilled;

        public int StarPowerActivationCount;

        public double TimeInStarPower;

        /// <summary>
        /// Amount of Star Power/Overdrive gained from whammy during the current whammy period.
        /// </summary>
        public uint StarPowerWhammyTicks;

        /// <summary>
        /// True if the player currently has Star Power/Overdrive active.
        /// </summary>
        public bool IsStarPowerActive;

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

        public int StarPowerScore;

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

            StarPowerTickAmount = stats.StarPowerTickAmount;
            TotalStarPowerTicks = stats.TotalStarPowerTicks;
            TotalStarPowerBarsFilled = stats.TotalStarPowerBarsFilled;
            StarPowerActivationCount = stats.StarPowerActivationCount;
            TimeInStarPower = stats.TimeInStarPower;
            StarPowerWhammyTicks = stats.StarPowerWhammyTicks;
            IsStarPowerActive = stats.IsStarPowerActive;

            StarPowerPhrasesHit = stats.StarPowerPhrasesHit;
            TotalStarPowerPhrases = stats.TotalStarPowerPhrases;

            SoloBonuses = stats.SoloBonuses;
            StarPowerScore = stats.StarPowerScore;

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

            StarPowerTickAmount = 0;
            TotalStarPowerTicks = 0;
            TotalStarPowerBarsFilled = 0;
            StarPowerActivationCount = 0;
            TimeInStarPower = 0;
            StarPowerWhammyTicks = 0;
            IsStarPowerActive = false;

            StarPowerPhrasesHit = 0;
            // TotalStarPowerPhrases = 0;

            SoloBonuses = 0;
            StarPowerScore = 0;

            Stars = 0;
        }
    }
}