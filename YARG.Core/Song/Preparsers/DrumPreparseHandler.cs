using System;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song.Preparsers
{
    public class DrumPreparseHandler
    {
        private DifficultyMask _validations;
        public DrumsType Type;

        public DifficultyMask ValidatedDiffs => _validations;

        public void ParseMidi(YARGMidiTrack track)
        {
            if (_validations > 0)
                return;

            if (Type == DrumsType.FiveLane)
                _validations = Midi_FiveLane_Preparser.Parse(track);
            else if (Type == DrumsType.ProDrums)
                _validations = Midi_FourLane_Preparser.ParseProDrums(track);
            else if (Type == DrumsType.FourLane)
                (_validations, Type) = Midi_FourLane_Preparser.ParseFourLane(track);
            else
            {
                (_validations, Type) = Midi_UnknownDrums_Preparser.Parse(track, Type);
                if (Type == DrumsType.UnknownPro)
                    Type = DrumsType.ProDrums;
                else if (Type == DrumsType.Unknown)
                    Type = DrumsType.FourLane;
            }
        }

        public bool ParseChart<TChar>(ref YARGTextContainer<TChar> container, Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            var diffMask = difficulty.ToDifficultyMask();
            if ((_validations & diffMask) > 0)
            {
                return false;
            }

            return Type switch
            {
                DrumsType.Unknown => ParseChartUnknown(ref container, diffMask),
                DrumsType.FourLane => ParseChartFourLane(ref container, diffMask),
                _ => ParseChartCommon(ref container, diffMask),
            };
        }

        private const int FOUR_LANE_COUNT = 4;
        private const int FIVE_LANE_COUNT = 5;
        private const int YELLOW_CYMBAL = 66;
        private const int GREEN_CYMBAL = 68;
        private const int DOUBLE_BASS_MODIFIER = 32;

        private bool ParseChartUnknown<TChar>(ref YARGTextContainer<TChar> container, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;

            DotChartEvent ev = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    int lane = YARGTextReader.ExtractInt32(ref container);
                    long _ = YARGTextReader.ExtractInt64(ref container);
                    if (lane <= FIVE_LANE_COUNT)
                    {
                        _validations |= difficulty;
                        found = true;

                        if (lane == FIVE_LANE_COUNT)
                            Type = DrumsType.FiveLane;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        Type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && Type != DrumsType.Unknown && !checkExpertPlus)
                        return true;
                }
            }
            return false;
        }

        private bool ParseChartFourLane<TChar>(ref YARGTextContainer<TChar> container, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;

            DotChartEvent ev = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    int lane = YARGTextReader.ExtractInt32(ref container);
                    long _ = YARGTextReader.ExtractInt64(ref container);
                    if (lane <= FOUR_LANE_COUNT)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        Type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && Type == DrumsType.ProDrums && !checkExpertPlus)
                        return true;
                }
            }
            return false;
        }

        private bool ParseChartCommon<TChar>(ref YARGTextContainer<TChar> container, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            int numPads = Type == DrumsType.ProDrums ? FOUR_LANE_COUNT : FIVE_LANE_COUNT;

            DotChartEvent ev = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    int lane = YARGTextReader.ExtractInt32(ref container);
                    long _ = YARGTextReader.ExtractInt64(ref container);
                    if (lane <= numPads)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && !checkExpertPlus)
                        return true;
                }
            }
            return false;
        }
    }
}