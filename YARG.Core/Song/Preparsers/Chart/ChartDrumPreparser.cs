﻿using System;
using System.Diagnostics;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public class ChartDrumPreparser
    {
        private byte _validations;
        private DrumType _type;

        public byte ValidatedDiffs => _validations;
        public DrumType Type => _type;

        public ChartDrumPreparser(DrumType type)
        {
            // We have to ignore the "pro_drums" modifier with .chart preparsing, at least initally
            Debug.Assert(type != DrumType.FOUR_PRO);
            _type = type;
            _validations = 0;
        }

        public bool Preparse(YARGChartFileReader reader)
        {
            int index = reader.Difficulty;
            int mask = 1 << index;
            if ((_validations & mask) > 0)
                return true;

            return _type switch
            {
                DrumType.UNKNOWN => PreparseUnknown(reader, index, (byte)mask),
                DrumType.FOUR_LANE => PreparseFourLane(reader, index, (byte) mask),
                _ => PreparseCommon(reader, index, (byte) mask),
            };
        }

        private bool PreparseUnknown(YARGChartFileReader reader, int index, byte mask)
        {
            bool found = false;
            bool expertPlus = index != 3;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= 5)
                    {
                        _validations |= mask;
                        found = true;

                        if (lane == 5)
                            _type = DrumType.FIVE_LANE;
                    }
                    else if (66 <= lane && lane <= 68)
                    {
                        _type = DrumType.FOUR_PRO;
                    }
                    else if (index == 3 && lane == 32)
                    {
                        expertPlus = true;
                        _validations |= 16;
                    }

                    if (found && _type != DrumType.FOUR_LANE && expertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool PreparseFourLane(YARGChartFileReader reader, int index, byte mask)
        {
            bool found = false;
            bool expertPlus = index != 3;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= 4)
                    {
                        found = true;
                        _validations |= mask;
                    }
                    else if (66 <= lane && lane <= 68)
                    {
                        _type = DrumType.FOUR_PRO;
                    }
                    else if (index == 3 && lane == 32)
                    {
                        expertPlus = true;
                        _validations |= 16;
                    }

                    if (found && _type != DrumType.FOUR_LANE && expertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool PreparseCommon(YARGChartFileReader reader, int index, byte mask)
        {
            bool found = false;
            bool expertPlus = index != 3;
            int numPads = _type == DrumType.FOUR_PRO ? 4 : 5;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= numPads)
                    {
                        found = true;
                        _validations |= mask;
                    }
                    else if (index == 3 && lane == 32)
                    {
                        expertPlus = true;
                        _validations |= 16;
                    }

                    if (found && expertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }
    }
}
