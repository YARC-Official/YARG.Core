namespace YARG.Core.Engine
{
    public abstract class BaseStats
    {
        /// <summary>
        /// The accumulated score (e.g from notes hit and sustains)
        /// </summary>
        public int Score;

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
        /// Number of notes which have been missed.
        /// </summary>
        public int NotesMissed;

        /// <summary>
        /// Amount of Star Power/Overdrive the player currently has.
        /// </summary>
        public double StarPowerAmount;

        /// <summary>
        /// True if the player currently has Star Power/Overdrive active.
        /// </summary>
        public bool IsStarPowerActive;

        /// <summary>
        /// Number of Star Power phrases which have been hit.
        /// </summary>
        public int PhrasesHit;

        /// <summary>
        /// Number of Star Power phrases which have been missed.
        /// </summary>
        public int PhrasesMissed;

        /// <summary>
        /// The number of stars the player has achieved.
        /// </summary>
        /// <remarks>This value should not be written to Replay files as Star Cutoffs may change over time.</remarks>
        public int Stars;

        protected BaseStats()
        {
        }

        protected BaseStats(BaseStats stats)
        {
            Score = stats.Score;
            Combo = stats.Combo;
            MaxCombo = stats.MaxCombo;
            ScoreMultiplier = stats.ScoreMultiplier;
            NotesHit = stats.NotesHit;
            NotesMissed = stats.NotesMissed;
            StarPowerAmount = stats.StarPowerAmount;
            IsStarPowerActive = stats.IsStarPowerActive;
            PhrasesHit = stats.PhrasesHit;
            PhrasesMissed = stats.PhrasesMissed;
            Stars = stats.Stars;
        }
    }
}