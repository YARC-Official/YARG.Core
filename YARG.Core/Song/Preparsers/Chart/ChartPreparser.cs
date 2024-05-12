using System;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class ChartPreparser
    {
        public static bool Preparse<TChar>(ref YARGTextContainer<TChar> container, Difficulty difficulty, ref PartValues scan, Func<int, bool> func)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (scan[difficulty])
                return true;

            DotChartEvent ev = default;
            DotChartNote note = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    note.Lane = YARGTextReader.ExtractInt32(ref container);
                    note.Duration = YARGTextReader.ExtractInt64(ref container);
                    if (func(note.Lane))
                    {
                        scan.SetDifficulty(difficulty);
                        return true;
                    }
                }
            }
            return false;
        }

        private const int KEYS_MAX = 5;
        private const int GUITAR_FIVEFRET_MAX = 5;
        private const int OPEN_NOTE = 7;
        private const int SIX_FRET_BLACK1 = 8;

        // Uses FiveFret parsing rules, but leaving this here just in case.
        //public static bool ValidateKeys(int lane)
        //{
        //    return lane < KEYS_MAX;
        //}

        public static bool ValidateSixFret(int lane)
        {
            return lane < GUITAR_FIVEFRET_MAX || lane == SIX_FRET_BLACK1 || lane == OPEN_NOTE;
        }

        public static bool ValidateFiveFret(int lane)
        {
            return lane < GUITAR_FIVEFRET_MAX || lane == OPEN_NOTE;
        }
    }
}
