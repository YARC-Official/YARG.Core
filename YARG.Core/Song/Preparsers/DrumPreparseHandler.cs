using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song.Preparsers
{
    public class DrumPreparseHandler
    {
        private byte _validations;
        private DrumType _type;

        public byte ValidatedDiffs => _validations;
        public DrumType Type => _type;

        public DrumPreparseHandler(DrumType type)
        {
            _type = type;
            _validations = 0;
        }

        public void ParseMidi(YARGMidiReader reader)
        {
            if (_validations > 0)
                return;

            if (_type == DrumType.FIVE_LANE)
                _validations = MidiInstrumentPreparser.Parse<Midi_FiveLaneDrum>(reader);
            else if (_type == DrumType.FOUR_LANE || _type == DrumType.FOUR_PRO)
            {
                var preparser = _type == DrumType.FOUR_PRO ? new Midi_ProDrum() : new Midi_FourLaneDrum();
                _validations = MidiInstrumentPreparser.Parse(preparser, reader);
                _type = preparser.Type;
            }
            else
            {
                Midi_UnknownDrums unknown = new(_type);
                _validations = MidiInstrumentPreparser.Parse(unknown, reader);
                _type = unknown.Type switch
                {
                    DrumType.UNKNOWN_PRO => DrumType.FOUR_PRO,
                    DrumType.UNKNOWN => DrumType.FOUR_LANE,
                    _ => unknown.Type,
                };
            }
        }

        public void ParseChart(YARGChartFileReader reader)
        {
            int index = reader.Difficulty;
            int mask = 1 << index;

            bool skip = true;
            if ((_validations & mask) == 0)
            {
                skip = _type switch
                {
                    DrumType.UNKNOWN => ParseChartUnknown(reader, index, (byte) mask),
                    DrumType.FOUR_LANE => ParseChartFourLane(reader, index, (byte) mask),
                    _ => ParseChartCommon(reader, index, (byte) mask),
                };
            }

            if (skip)
                reader.SkipTrack();
        }

        private bool ParseChartUnknown(YARGChartFileReader reader, int index, byte mask)
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

                    if (found && _type != DrumType.UNKNOWN && expertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartFourLane(YARGChartFileReader reader, int index, byte mask)
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

                    if (found && _type == DrumType.FOUR_PRO && expertPlus)
                        return true;
                }
                reader.NextEvent();
            }
            return false;
        }

        private bool ParseChartCommon(YARGChartFileReader reader, int index, byte mask)
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
