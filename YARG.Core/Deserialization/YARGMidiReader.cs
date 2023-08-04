using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Core.Deserialization
{
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

    public struct MidiParseEvent
    {
        public long position;
        public MidiEventType type;
        public int channel;
    };

    public enum MidiTrackType
    {
        Events,
        Guitar_5,
        Guitar_6,
        Bass_5,
        Bass_6,
        Rhythm,
        Coop,
        Keys,
        Drums,
        Vocals,
        Harm1,
        Harm2,
        Harm3,
        Real_Guitar,
        Real_Guitar_22,
        Real_Bass,
        Real_Bass_22,
        Real_Keys_X,
        Real_Keys_H,
        Real_Keys_M,
        Real_Keys_E,
        Beat,
        Unknown
    }

    public class YARGMidiReader
    {
        public static readonly Dictionary<string, MidiTrackType> TRACKNAMES = new()
        {
            {"EVENTS",              MidiTrackType.Events},
            {"PART GUITAR",         MidiTrackType.Guitar_5},
            {"T1 GEMS",             MidiTrackType.Guitar_5},
            {"PART GUITAR GHL",     MidiTrackType.Guitar_6},
            {"PART BASS",           MidiTrackType.Bass_5},
            {"PART BASS GHL",       MidiTrackType.Bass_6},
            {"PART RHYTHM",         MidiTrackType.Rhythm},
            {"PART GUITAR COOP",    MidiTrackType.Coop},
            {"PART KEYS",           MidiTrackType.Keys},
            {"PART DRUMS",          MidiTrackType.Drums},
            {"PART VOCALS",         MidiTrackType.Vocals},
            {"PART HARM1",          MidiTrackType.Harm1},
            {"PART HARM2",          MidiTrackType.Harm2},
            {"PART HARM3",          MidiTrackType.Harm3},
            {"HARM1",               MidiTrackType.Harm1},
            {"HARM2",               MidiTrackType.Harm2},
            {"HARM3",               MidiTrackType.Harm3},
            {"PART REAL_GUITAR",    MidiTrackType.Real_Guitar},
            {"PART REAL_GUITAR_22", MidiTrackType.Real_Guitar_22},
            {"PART REAL_BASS",      MidiTrackType.Real_Bass},
            {"PART REAL_BASS_22",   MidiTrackType.Real_Bass_22},
            {"PART REAL_KEYS_X",    MidiTrackType.Real_Keys_X},
            {"PART REAL_KEYS_H",    MidiTrackType.Real_Keys_H},
            {"PART REAL_KEYS_M",    MidiTrackType.Real_Keys_M},
            {"PART REAL_KEYS_E",    MidiTrackType.Real_Keys_E},
            {"BEAT",                MidiTrackType.Beat},
        };

        internal static readonly byte[][] TRACKTAGS = { Encoding.ASCII.GetBytes("MThd"), Encoding.ASCII.GetBytes("MTrk") };

        static YARGMidiReader() { }

        private struct MidiHeader
        {
            public ushort format;
            public ushort numTracks;
            public ushort tickRate;
        };
        private MidiHeader header;
        private ushort trackCount = 0;

        private MidiParseEvent currentEvent;
        private MidiEventType midiEvent = MidiEventType.Reset_Or_Meta;
        private int runningOffset;

        private readonly byte multiplierNote;
        private readonly YARGBinaryReader reader;

        public YARGMidiReader(YARGBinaryReader reader, byte multiplierNote = 116)
        {
            this.reader = reader;
            this.multiplierNote = multiplierNote;
            ProcessHeaderChunk();
        }

        public YARGMidiReader(YARGFile file, byte multiplierNote = 116) : this(new YARGBinaryReader(file), multiplierNote) { }

        public YARGMidiReader(byte[] data, byte multiplierNote = 116) : this(new YARGBinaryReader(data), multiplierNote) { }

        public YARGMidiReader(string path, byte multiplierNote = 116) : this(new YARGBinaryReader(path), multiplierNote) { }

        public bool StartTrack()
        {
            if (trackCount == header.numTracks)
                return false;

            if (currentEvent.type != MidiEventType.Reset_Or_Meta)
                reader.ExitSection();

            reader.ExitSection();
            trackCount++;

            if (!reader.CompareTag(TRACKTAGS[1]))
                throw new Exception($"Midi Track Tag 'MTrk' not found for Track '{trackCount}'");

            reader.EnterSection((int) reader.ReadUInt32(Endianness.BigEndian));

            currentEvent.position = 0;
            currentEvent.type = MidiEventType.Reset_Or_Meta;

            int start = reader.Position;
            if (!TryParseEvent() || currentEvent.type != MidiEventType.Text_TrackName)
            {
                reader.ExitSection();
                reader.Position = start;
                currentEvent.position = 0;
                currentEvent.type = MidiEventType.Reset_Or_Meta;
            }
            return true;
        }

        public bool TryParseEvent()
        {
            if (currentEvent.type != MidiEventType.Reset_Or_Meta)
                reader.ExitSection();

            currentEvent.position += reader.ReadVLQ();
            byte tmp = reader.PeekByte();
            var type = (MidiEventType) tmp;
            if (type < MidiEventType.Note_Off)
            {
                if (midiEvent == MidiEventType.Reset_Or_Meta)
                    throw new Exception("Invalid running event");
                currentEvent.type = midiEvent;
                reader.EnterSection(runningOffset);
            }
            else
            {
                reader.Move_Unsafe(1);
                if (type < MidiEventType.SysEx)
                {
                    currentEvent.channel = (byte) (tmp & 15);
                    midiEvent = (MidiEventType) (tmp & 240);
                    runningOffset = midiEvent switch
                    {
                        MidiEventType.Note_On => 2,
                        MidiEventType.Note_Off => 2,
                        MidiEventType.Control_Change => 2,
                        MidiEventType.Key_Pressure => 2,
                        MidiEventType.Pitch_Wheel => 2,
                        _ => 1
                    };
                    currentEvent.type = midiEvent;
                    reader.EnterSection(runningOffset);
                }
                else
                {
                    switch (type)
                    {
                        case MidiEventType.Reset_Or_Meta:
                            type = (MidiEventType) reader.ReadByte();
                            goto case MidiEventType.SysEx_End;
                        case MidiEventType.SysEx:
                        case MidiEventType.SysEx_End:
                            reader.EnterSection((int) reader.ReadVLQ());
                            break;
                        case MidiEventType.Song_Position:
                            reader.EnterSection(2);
                            break;
                        case MidiEventType.Song_Select:
                            reader.EnterSection(1);
                            break;
                        default:
                            reader.EnterSection(0);
                            break;
                    }
                    currentEvent.type = type;

                    if (currentEvent.type == MidiEventType.End_Of_Track)
                        return false;
                }
            }
            return true;
        }

        public ref MidiParseEvent GetParsedEvent() { return ref currentEvent; }
        public ushort GetTrackNumber() { return trackCount; }
        public MidiParseEvent GetEvent() { return currentEvent; }

        public ReadOnlySpan<byte> ExtractTextOrSysEx()
        {
            return reader.ReadSpan(reader.Boundary - reader.Position);
        }

        public void ExtractMidiNote(ref MidiNote note)
        {
            note.value = reader.ReadByte();
            note.velocity = reader.ReadByte();
        }

        private void ProcessHeaderChunk()
        {
            if (!reader.CompareTag(TRACKTAGS[0]))
                throw new Exception("Midi Header Chunk Tag 'MTrk' not found");

            reader.EnterSection((int) reader.ReadUInt32(Endianness.BigEndian));
            header.format = reader.ReadUInt16(Endianness.BigEndian);
            header.numTracks = reader.ReadUInt16(Endianness.BigEndian);
            header.tickRate = reader.ReadUInt16(Endianness.BigEndian);
            currentEvent.type = MidiEventType.Reset_Or_Meta;
        }
    };
}
