﻿namespace YARG.Core.Engine
{
    public abstract class BaseStats
    {
        public int Score;
        public int Combo;
        public int MaxCombo;

        public int ScoreMultiplier;

        public int NotesHit;
        public int NotesMissed;

        public double StarPowerAmount;

        public bool IsStarPowerActive;

        public int PhrasesHit;
        public int PhrasesMissed;

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
        }

        protected BaseStats(int score, int combo, int maxCombo, int scoreMultiplier, int notesHit, int notesMissed,
            double starPowerAmount, bool isStarPowerActive, int phrasesHit)
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