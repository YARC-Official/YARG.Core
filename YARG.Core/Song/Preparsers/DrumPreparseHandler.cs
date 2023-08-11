using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song.Preparsers
{
    /// <summary>
    /// Available Drum types. Unknown values for preparse purposes
    /// </summary>
    public enum DrumPreparseType
    {
        FourLane,
        FourPro,
        FiveLane,
        Unknown,
        UnknownPro,
    }

    public class DrumPreparseHandler
    {
        private DifficultyMask _validations;
        private DrumPreparseType _type;

        public DifficultyMask ValidatedDiffs => _validations;
        public DrumPreparseType Type => _type;

        public DrumPreparseHandler(DrumPreparseType type)
        {
            _type = type;
            _validations = 0;
        }

        public void ParseMidi(YARGMidiReader reader)
        {
            if (_validations > 0)
                return;

            if (_type == DrumPreparseType.FiveLane)
                _validations = (DifficultyMask) MidiInstrumentPreparser.Parse<Midi_FiveLaneDrum>(reader);
            else if (_type == DrumPreparseType.FourLane || _type == DrumPreparseType.FourPro)
            {
                var preparser = _type == DrumPreparseType.FourPro ? new Midi_ProDrum() : new Midi_FourLaneDrum();
                _validations = (DifficultyMask) MidiInstrumentPreparser.Parse(preparser, reader);
                _type = preparser.Type;
            }
            else
            {
                Midi_UnknownDrums unknown = new(_type);
                _validations = (DifficultyMask) MidiInstrumentPreparser.Parse(unknown, reader);
                _type = unknown.Type switch
                {
                    DrumPreparseType.UnknownPro => DrumPreparseType.FourPro,
                    DrumPreparseType.Unknown => DrumPreparseType.FourLane,
                    _ => unknown.Type,
                };
            }
        }

        public void ParseChart(IYARGChartReader reader)
        {
            DifficultyMask difficulty = (DifficultyMask)(1 << reader.Difficulty);

            bool skip = true;
            if ((_validations & difficulty) == 0)
            {
                skip = _type switch
                {
                    DrumPreparseType.Unknown => ParseChartUnknown(reader, difficulty),
                    DrumPreparseType.FourLane => ParseChartFourLane(reader, difficulty),
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
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= FIVE_LANE_COUNT)
                    {
                        _validations |= difficulty;
                        found = true;

                        if (lane == FIVE_LANE_COUNT)
                            _type = DrumPreparseType.FiveLane;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        _type = DrumPreparseType.FourPro;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && _type != DrumPreparseType.Unknown && !checkExpertPlus)
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
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= FOUR_LANE_COUNT)
                    {
                        found = true;
                        _validations |= difficulty;
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        _type = DrumPreparseType.FourPro;
                    }
                    else if (checkExpertPlus && lane == DOUBLE_BASS_MODIFIER)
                    {
                        checkExpertPlus = false;
                        _validations |= DifficultyMask.ExpertPlus;
                    }

                    if (found && _type == DrumPreparseType.FourPro && !checkExpertPlus)
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
            int numPads = _type == DrumPreparseType.FourPro ? FOUR_LANE_COUNT : FIVE_LANE_COUNT;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
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
