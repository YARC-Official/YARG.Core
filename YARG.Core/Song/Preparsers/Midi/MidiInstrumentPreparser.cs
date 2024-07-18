using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class MidiPreparser_Constants
    {
        public const DifficultyMask ALL_DIFFICULTIES = DifficultyMask.Easy | DifficultyMask.Medium | DifficultyMask.Hard | DifficultyMask.Expert;
        public const DifficultyMask ALL_DIFFICULTIES_PLUS = ALL_DIFFICULTIES | DifficultyMask.ExpertPlus;

        public const int DEFAULT_NOTE_MIN = 60;
        public const int DEFAULT_MAX = 100;
        public const int NUM_DIFFICULTIES = 4;
        public const int NOTES_PER_DIFFICULTY = 12;

        public static readonly int[] DIFF_INDICES = new int[NOTES_PER_DIFFICULTY * NUM_DIFFICULTIES]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
    }
}
