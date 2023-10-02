using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
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

    public sealed class YARGMidiReader
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

        private readonly Stream stream;
        private YARGBinaryReader trackReader;
        private int nextEvent;

        public YARGMidiReader(Stream stream)
        {
            this.stream = stream;
            ProcessHeaderChunk();
        }

        public YARGMidiReader(byte[] data) : this(new MemoryStream(data, 0, data.Length, false, true)) { }

        public YARGMidiReader(string path) : this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        [MemberNotNullWhen(true, nameof(trackReader))]
        private bool LoadTrack(Span<byte> tag)
        {
            Span<byte> tagBuffer = stackalloc byte[4];
            stream.Read(tagBuffer);
            if (!tagBuffer.SequenceEqual(tag))
                return false;

            trackReader = new YARGBinaryReader(stream, stream.ReadInt32BE());
            return true;
        }

        public bool StartTrack()
        {
            if (trackCount == header.numTracks || stream.Position == stream.Length)
                return false;

            trackCount++;
            if (!LoadTrack(TRACKTAGS[1]))
                throw new Exception($"Midi Track Tag 'MTrk' not found for Track '{trackCount}'");

            currentEvent.position = 0;
            currentEvent.type = MidiEventType.Reset_Or_Meta;
            nextEvent = 0;

            if (!TryParseEvent() || currentEvent.type != MidiEventType.Text_TrackName)
            {
                nextEvent = 0;
                currentEvent.position = 0;
                currentEvent.type = MidiEventType.Reset_Or_Meta;
            }
            return true;
        }

        public bool TryParseEvent(ref MidiParseEvent ev)
        {
            if (!TryParseEvent())
                return false;

            ev = currentEvent;
            return true;
        }

        private bool TryParseEvent()
        {
            trackReader.Position = nextEvent;
            currentEvent.position += trackReader.ReadVLQ();
            byte tmp = trackReader.PeekByte();
            var type = (MidiEventType) tmp;
            int eventLength;
            if (type < MidiEventType.Note_Off)
            {
                if (midiEvent == MidiEventType.Reset_Or_Meta)
                    throw new Exception("Invalid running event");
                currentEvent.type = midiEvent;
                eventLength = runningOffset;
            }
            else
            {
                trackReader.Move_Unsafe(1);
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
                    eventLength = runningOffset;
                }
                else
                {
                    switch (type)
                    {
                        case MidiEventType.Reset_Or_Meta:
                            type = (MidiEventType) trackReader.ReadByte();
                            goto case MidiEventType.SysEx_End;
                        case MidiEventType.SysEx:
                        case MidiEventType.SysEx_End:
                            eventLength = (int)trackReader.ReadVLQ();
                            break;
                        case MidiEventType.Song_Position:
                            eventLength = 2;
                            break;
                        case MidiEventType.Song_Select:
                            eventLength = 1;
                            break;
                        default:
                            eventLength = 0;
                            break;
                    }
                    currentEvent.type = type;

                    if (currentEvent.type == MidiEventType.End_Of_Track)
                        return false;
                }
            }
            nextEvent = trackReader.Position + eventLength;
            return true;
        }

        public ushort GetTrackNumber() { return trackCount; }
        public MidiEventType GetEventType() { return currentEvent.type; }

        public ReadOnlySpan<byte> ExtractTextOrSysEx()
        {
            return trackReader.ReadSpan(nextEvent - trackReader.Position);
        }

        public void ExtractMidiNote(ref MidiNote note)
        {
            note.value = trackReader.ReadByte();
            note.velocity = trackReader.ReadByte();
        }

        [MemberNotNull(nameof(trackReader))]
        private void ProcessHeaderChunk()
        {
            if (!LoadTrack(TRACKTAGS[0]))
                throw new Exception("Midi Header Chunk Tag 'MTrk' not found");

            header.format = trackReader.ReadUInt16(Endianness.BigEndian);
            header.numTracks = trackReader.ReadUInt16(Endianness.BigEndian);
            header.tickRate = trackReader.ReadUInt16(Endianness.BigEndian);
        }
    };
}
