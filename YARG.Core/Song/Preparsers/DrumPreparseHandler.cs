using System;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song.Preparsers
{
    public class DrumPreparseHandler
    {
        private DifficultyMask _validations;
        private DrumsType _type;

        public DifficultyMask ValidatedDiffs => _validations;
        public DrumsType Type => _type;

        public DrumPreparseHandler(DrumsType type)
        {
            _type = type;
            _validations = 0;
        }

        public void ParseMidi(YARGMidiTrack track)
        {
            if (_validations > 0)
                return;

            if (_type == DrumsType.FiveLane)
                _validations = Midi_FiveLane_Preparser.Parse(track);
            else if (_type == DrumsType.ProDrums)
                _validations = Midi_FourLane_Preparser.ParseProDrums(track);
            else if (_type == DrumsType.FourLane)
                (_validations, _type) = Midi_FourLane_Preparser.ParseFourLane(track);
            else
            {
                (_validations, _type) = Midi_UnknownDrums_Preparser.Parse(track, _type);
                if (_type == DrumsType.UnknownPro)
                    _type = DrumsType.ProDrums;
                else if (_type == DrumsType.Unknown)
                    _type = DrumsType.FourLane;
            }
        }

        public void ParseChart<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : IStringDecoder<TChar>, new()
        {
            var difficulty = reader.Difficulty.ToDifficultyMask();

            bool skip = true;
            if ((_validations & difficulty) == 0)
            {
                skip = _type switch
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

        private bool ParseChartUnknown<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : IStringDecoder<TChar>, new()
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
                            _type = DrumsType.FiveLane;
                    }
                    else if (YELLOW_CYMBAL <= note.Lane && note.Lane <= GREEN_CYMBAL)
                    {
                        _type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && note.Lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && _type != DrumsType.Unknown && !checkExpertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartFourLane<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : IStringDecoder<TChar>, new()
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
                        _type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && note.Lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && _type == DrumsType.ProDrums && !checkExpertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartCommon<TChar, TBase, TDecoder>(YARGChartFileReader<TChar, TBase, TDecoder> reader, DifficultyMask difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : IStringDecoder<TChar>, new()
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            int numPads = _type == DrumsType.ProDrums ? FOUR_LANE_COUNT : FIVE_LANE_COUNT;

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
