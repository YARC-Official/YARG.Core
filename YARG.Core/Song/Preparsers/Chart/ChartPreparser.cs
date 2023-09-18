using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public static class ChartPreparser
    {
        public static bool Preparse(IYARGChartReader reader, ref PartValues scan, Func<int, bool> func)
        {
            var difficulty = reader.Difficulty;
            if (scan[difficulty])
                return true;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEventType.Note)
                {
                    if (func(reader.ExtractLaneAndSustain().Item1))
                    {
                        scan.SetDifficulty(difficulty);
                        return true;
                    }
                }
                reader.NextEvent();
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
