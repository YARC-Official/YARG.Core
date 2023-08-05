using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public static class ChartPreparser
    {
        public static bool Preparse(YARGChartFileReader reader, ref PartValues scan, Func<int, bool> func)
        {
            int index = reader.Difficulty;
            if (scan[index])
                return true;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    if (func(reader.ExtractLaneAndSustain().Item1))
                    {
                        scan.Set(index);
                        return true;
                    }
                }
                reader.NextEvent();
            }
            return false;
        }

        public static bool ValidateKeys(int lane)
        {
            return lane < 5;
        }

        public static bool ValidateSixFret(int lane)
        {
            return lane < 5 || lane == 8 || lane == 7;
        }

        public static bool ValidateFiveFret(int lane)
        {
            return lane < 5 || lane == 7;
        }

        public static bool ValidateFiveLaneDrums(int lane)
        {
            return lane < 6;
        }

        public static bool ValidateFourLaneProDrums(int lane)
        {
            return lane < 5;
        }
    }
}
