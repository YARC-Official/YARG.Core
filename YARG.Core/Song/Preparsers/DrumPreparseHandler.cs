using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;

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

        public void ParseMidi(YARGMidiReader reader)
        {
            if (_validations > 0)
                return;

            if (_type == DrumsType.FiveLane)
                _validations = Midi_FiveLaneDrum.Parse(reader);
            else if (_type == DrumsType.ProDrums)
                _validations = Midi_FourLaneDrum.ParseProDrums(reader);
            else if (_type == DrumsType.FourLane)
                (_validations, _type) = Midi_FourLaneDrum.ParseFourLane(reader);
            else
            {
                (_validations, _type) = Midi_UnknownDrums.Parse(reader, _type);
                if (_type == DrumsType.UnknownPro)
                    _type = DrumsType.ProDrums;
                else if (_type == DrumsType.Unknown)
                    _type = DrumsType.FourLane;
            }
        }

        public void ParseChart(IYARGChartReader reader)
        {
            var difficulty = (DifficultyMask)(1 << reader.Difficulty);

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

        private bool ParseChartUnknown(IYARGChartReader reader, DifficultyMask difficulty)
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent_FW.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= FIVE_LANE_COUNT)
                    {
                        _validations |= difficulty;
                        found = true;

                        if (lane == FIVE_LANE_COUNT)
                            _type = DrumsType.FiveLane;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        _type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
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

        private bool ParseChartFourLane(IYARGChartReader reader, DifficultyMask difficulty)
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent_FW.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= FOUR_LANE_COUNT)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        _type = DrumsType.ProDrums;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
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

        private bool ParseChartCommon(IYARGChartReader reader, DifficultyMask difficulty)
        {
            bool found = false;
            bool checkExpertPlus = difficulty == DifficultyMask.Expert;
            int numPads = _type == DrumsType.ProDrums ? FOUR_LANE_COUNT : FIVE_LANE_COUNT;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent_FW.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
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
                reader.NextEvent();
            }
            return false;
        }
    }
}
