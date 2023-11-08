using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGMidiTrack
    {
        public static readonly Dictionary<string, MidiTrackType> TRACKNAMES = new()
        {
            {"EVENTS",               MidiTrackType.Events},
            {"PART GUITAR",          MidiTrackType.Guitar_5},
            {"T1 GEMS",              MidiTrackType.Guitar_5},
            {"PART GUITAR GHL",      MidiTrackType.Guitar_6},
            {"PART BASS",            MidiTrackType.Bass_5},
            {"PART BASS GHL",        MidiTrackType.Bass_6},
            {"PART RHYTHM",          MidiTrackType.Rhythm_5},
            {"PART RHYTHM GHL",      MidiTrackType.Rhythm_6},
            {"PART GUITAR COOP",     MidiTrackType.Coop_5},
            {"PART GUITAR COOP GHL", MidiTrackType.Coop_6},
            {"PART KEYS",            MidiTrackType.Keys},
            {"PART DRUMS",           MidiTrackType.Drums},
            {"PART VOCALS",          MidiTrackType.Vocals},
            {"PART HARM1",           MidiTrackType.Harm1},
            {"PART HARM2",           MidiTrackType.Harm2},
            {"PART HARM3",           MidiTrackType.Harm3},
            {"HARM1",                MidiTrackType.Harm1},
            {"HARM2",                MidiTrackType.Harm2},
            {"HARM3",                MidiTrackType.Harm3},
            {"PART REAL_GUITAR",     MidiTrackType.Pro_Guitar_17},
            {"PART REAL_GUITAR_22",  MidiTrackType.Pro_Guitar_22},
            {"PART REAL_BASS",       MidiTrackType.Pro_Bass_17},
            {"PART REAL_BASS_22",    MidiTrackType.Pro_Bass_22},
            {"PART REAL_KEYS_X",     MidiTrackType.Pro_Keys_X},
            {"PART REAL_KEYS_H",     MidiTrackType.Pro_Keys_H},
            {"PART REAL_KEYS_M",     MidiTrackType.Pro_Keys_M},
            {"PART REAL_KEYS_E",     MidiTrackType.Pro_Keys_E},
            {"BEAT",                 MidiTrackType.Beat},
        };

        private struct MidiEvent
        {
            public MidiEventType Type;
            public int Channel;
            public int Length;
        }

        private long _position;
        private MidiEvent _event;
        private MidiEvent _running;

        private YARGBinaryReader _reader;

        public long Position => _position;
        public MidiEventType Type => _event.Type;
        public int Channel => _event.Channel;

        public YARGMidiTrack(Stream stream)
        {
            _reader = new YARGBinaryReader(stream, stream.ReadInt32BE());
            if (!ParseEvent() || _event.Type != MidiEventType.Text_TrackName)
            {
                _reader.Position = 0;
                _position = 0;
                _event.Type = MidiEventType.Reset_Or_Meta;
                _running.Type = MidiEventType.Reset_Or_Meta;
            }
        }

        public bool ParseEvent()
        {
            _position += _reader.ReadVLQ();
            byte tmp = _reader.PeekByte();
            var type = (MidiEventType) tmp;
            if (type < MidiEventType.Note_Off)
            {
                if (_running.Type == MidiEventType.Reset_Or_Meta)
                    throw new Exception("Invalid running event");
                _event = _running;
            }
            else
            {
                _reader.Move_Unsafe(1);
                if (type < MidiEventType.SysEx)
                {
                    _running.Channel = (byte) (tmp & 15);
                    _running.Type = (MidiEventType) (tmp & 240);
                    _running.Length = _running.Type switch
                    {
                        MidiEventType.Note_On => 2,
                        MidiEventType.Note_Off => 2,
                        MidiEventType.Control_Change => 2,
                        MidiEventType.Key_Pressure => 2,
                        MidiEventType.Pitch_Wheel => 2,
                        _ => 1
                    };
                    _event = _running;
                }
                else
                {
                    switch (type)
                    {
                        case MidiEventType.Reset_Or_Meta:
                            type = (MidiEventType) _reader.ReadByte();
                            goto case MidiEventType.SysEx_End;
                        case MidiEventType.SysEx:
                        case MidiEventType.SysEx_End:
                            _event.Length = (int) _reader.ReadVLQ();
                            break;
                        case MidiEventType.Song_Position:
                            _event.Length = 2;
                            break;
                        case MidiEventType.Song_Select:
                            _event.Length = 1;
                            break;
                        default:
                            _event.Length = 0;
                            break;
                    }
                    if (type == MidiEventType.End_Of_Track)
                        return false;
                    _event.Type = type;
                }
            }
            return true;
        }

        public ReadOnlySpan<byte> ExtractTextOrSysEx()
        {
            return _reader.ReadSpan(_event.Length);
        }

        public void ExtractMidiNote(ref MidiNote note)
        {
            note.value = _reader.ReadByte();
            note.velocity = _reader.ReadByte();
        }

        public void SkipEvent()
        {
            _reader.Position += _event.Length;
        }
    }

    public enum MidiTrackType
    {
        Events,
        Guitar_5,
        Guitar_6,
        Bass_5,
        Bass_6,
        Rhythm_5,
        Rhythm_6,
        Coop_5,
        Coop_6,
        Keys,
        Drums,
        Vocals,
        Harm1,
        Harm2,
        Harm3,
        Pro_Guitar_17,
        Pro_Guitar_22,
        Pro_Bass_17,
        Pro_Bass_22,
        Pro_Keys_E,
        Pro_Keys_M,
        Pro_Keys_H,
        Pro_Keys_X,
        Beat,
        Unknown
    }

    public enum MidiEventType : byte
    {
        Sequence_Number = 0x00,
        Text = 0x01,
        Text_Copyright = 0x02,
        Text_TrackName = 0x03,
        Text_InstrumentName = 0x04,
        Text_Lyric = 0x05,
        Text_Marker = 0x06,
        Text_CuePoint = 0x07,
        Text_EnumLimit = 0x0F,
        MIDI_Channel_Prefix = 0x20,
        End_Of_Track = 0x2F,
        Tempo = 0x51,
        SMPTE_Offset = 0x54,
        Time_Sig = 0x58,
        Key_Sig = 0x59,
        Sequencer_Specific_Meta_Event = 0x7F,

        Note_Off = 0x80,
        Note_On = 0x90,
        Key_Pressure = 0xA0,
        Control_Change = 0xB0,
        Program_Change = 0xC0,
        Channel_Pressure = 0xD0,
        Pitch_Wheel = 0xE0,

        SysEx = 0xF0,
        Undefined = 0xF1,
        Song_Position = 0xF2,
        Song_Select = 0xF3,
        Undefined_2 = 0xF4,
        Undefined_3 = 0xF5,
        Tune_Request = 0xF6,
        SysEx_End = 0xF7,
        Timing_Clock = 0xF8,
        Undefined_4 = 0xF9,
        Start_Sequence = 0xFA,
        Continue_Sequence = 0xFB,
        Stop_Sequence = 0xFC,
        Undefined_5 = 0xFD,
        Active_Sensing = 0xFE,
        Reset_Or_Meta = 0xFF,
    };

    public struct MidiNote
    {
        public int value;
        public int velocity;
    };
}
