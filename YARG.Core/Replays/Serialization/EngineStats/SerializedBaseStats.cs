namespace YARG.Core.Replays.Serialization
{
    internal class SerializedBaseStats
    {
        public int CommittedScore;
        public int PendingScore;
        public int NoteScore;
        public int SustainScore;
        public int MultiplierScore;
        public int Combo;
        public int MaxCombo;
        public int ScoreMultiplier;
        public int NotesHit;
        public int TotalNotes;

        public uint StarPowerTickAmount;
        public uint TotalStarPowerTicks;

        public double TotalStarPowerBarsFilled;

        public int StarPowerActivationCount;

        public double TimeInStarPower;

        public uint StarPowerWhammyTicks;

        public bool IsStarPowerActive;

        public int StarPowerPhrasesHit;
        public int TotalStarPowerPhrases;
        public int SoloBonuses;
        public int StarPowerScore;

        public float Stars;
    }
}