namespace YARG.Core.Engine
{
    public class BaseStats
    {
        public int Score    { get; set; }
        public int Combo    { get; set; }
        public int MaxCombo { get; set; }

        public int ScoreMultiplier { get; set; }

        public int NotesHit    { get; set; }
        public int NotesMissed { get; set; }

        public double StarPowerAmount { get; set; }

        public bool IsStarPowerActive { get; set; }

        public int PhrasesHit { get; set; }

        public BaseStats()
        {
        }
    
        public BaseStats(int score, int combo, int maxCombo, int scoreMultiplier, int notesHit, int notesMissed,
            double           starPowerAmount, bool isStarPowerActive, int phrasesHit)
        {
            Score = score;
            Combo = combo;
            MaxCombo = maxCombo;
            ScoreMultiplier = scoreMultiplier;
            NotesHit = notesHit;
            NotesMissed = notesMissed;
            StarPowerAmount = starPowerAmount;
            IsStarPowerActive = isStarPowerActive;
            PhrasesHit = phrasesHit;
        }
    }
}