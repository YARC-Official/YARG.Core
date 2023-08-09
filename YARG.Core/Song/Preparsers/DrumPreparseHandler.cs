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
        private byte _validations;
        private DrumPreparseType _type;

        public byte ValidatedDiffs => _validations;
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
                _validations = MidiInstrumentPreparser.Parse<Midi_FiveLaneDrum>(reader);
            else if (_type == DrumPreparseType.FourLane || _type == DrumPreparseType.FourPro)
            {
                var preparser = _type == DrumPreparseType.FourPro ? new Midi_ProDrum() : new Midi_FourLaneDrum();
                _validations = MidiInstrumentPreparser.Parse(preparser, reader);
                _type = preparser.Type;
            }
            else
            {
                Midi_UnknownDrums unknown = new(_type);
                _validations = MidiInstrumentPreparser.Parse(unknown, reader);
                _type = unknown.Type switch
                {
                    DrumPreparseType.UnknownPro => DrumPreparseType.FourPro,
                    DrumPreparseType.Unknown => DrumPreparseType.FourLane,
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
                    DrumPreparseType.Unknown => ParseChartUnknown(reader, index, (byte) mask),
                    DrumPreparseType.FourLane => ParseChartFourLane(reader, index, (byte) mask),
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
                            _type = DrumPreparseType.FiveLane;
                    }
                    else if (66 <= lane && lane <= 68)
                    {
                        _type = DrumPreparseType.FourPro;
                    }
                    else if (index == 3 && lane == 32)
                    {
                        expertPlus = true;
                        _validations |= 16;
                    }

                    if (found && _type != DrumPreparseType.Unknown && expertPlus)
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
                        _type = DrumPreparseType.FourPro;
                    }
                    else if (index == 3 && lane == 32)
                    {
                        expertPlus = true;
                        _validations |= 16;
                    }

                    if (found && _type == DrumPreparseType.FourPro && expertPlus)
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
            int numPads = _type == DrumPreparseType.FourPro ? 4 : 5;
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
