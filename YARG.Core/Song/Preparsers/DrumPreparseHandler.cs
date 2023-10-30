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

        public void ParseChart<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            var difficulty = reader.Difficulty.ToDifficultyMask();

            bool skip = true;
            if ((_validations & difficulty) == 0)
            {
                skip = Type switch
                {
                    DrumsType.Unknown => ParseChartUnknown(reader, difficulty),
                    DrumsType.FourLane => ParseChartFourLane(reader, difficulty),
                    _ => ParseChartCommon(reader, difficulty),
                };
            }

            if (skip)
                reader.SkipTrack();
        }

        private const int FOUR_LANE_COUNT = 4;
        private const int FIVE_LANE_COUNT = 5;
        private const int YELLOW_CYMBAL = 66;
        private const int GREEN_CYMBAL = 68;
        private const int DOUBLE_BASS_MODIFIER = 32;

        private bool ParseChartUnknown<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;

            DotChartEvent ev = default;
            DotChartNote note = default;
            while (reader.TryParseEvent(ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    reader.ExtractLaneAndSustain(ref note);
                    if (note.Lane <= FIVE_LANE_COUNT)
                    {
                        _validations |= difficulty;
                        found = true;

                        if (note.Lane == FIVE_LANE_COUNT)
                            Type = DrumsType.FiveLane;
                    }
                    else if (YELLOW_CYMBAL <= note.Lane && note.Lane <= GREEN_CYMBAL)
                    {
                        Type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && note.Lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && Type != DrumsType.Unknown && !checkExpertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartFourLane<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;

            DotChartEvent ev = default;
            DotChartNote note = default;
            while (reader.TryParseEvent(ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    reader.ExtractLaneAndSustain(ref note);
                    if (note.Lane <= FOUR_LANE_COUNT)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (YELLOW_CYMBAL <= note.Lane && note.Lane <= GREEN_CYMBAL)
                    {
                        Type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && note.Lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && Type == DrumsType.ProDrums && !checkExpertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartCommon<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            int numPads = Type == DrumsType.ProDrums ? FOUR_LANE_COUNT : FIVE_LANE_COUNT;

            DotChartEvent ev = default;
            DotChartNote note = default;
            while (reader.TryParseEvent(ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    reader.ExtractLaneAndSustain(ref note);
                    if (note.Lane <= numPads)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (checkExpertPlus && note.Lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && !checkExpertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }
    }
}
