using System;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public unsafe class Midi_FiveFret : MidiInstrument_Common
    {
        private const int FIVEFRET_MIN = 59;
        // Open note included
        private const int NUM_LANES = 6;
        private static readonly byte[][] ENHANCED_STRINGS = new byte[][] { Encoding.ASCII.GetBytes("[ENHANCED_OPENS]"), Encoding.ASCII.GetBytes("ENHANCED_OPENS") };

        private readonly bool[,] statuses = new bool[NUM_DIFFICULTIES, NUM_LANES];
        private readonly int[] laneIndices = new int[NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        private Midi_FiveFret() { }

        public static byte Parse(YARGMidiReader reader)
        {
            Midi_FiveFret preparser = new();
            preparser.Process(reader);
            return (byte) preparser.validations;
        }

        protected override bool IsNote() { return FIVEFRET_MIN <= note.value && note.value <= DEFAULT_MAX; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - FIVEFRET_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = laneIndices[noteValue];
                if (laneIndex < NUM_LANES)
                    statuses[diffIndex, laneIndex] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            int noteValue = note.value - FIVEFRET_MIN;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficultyTracker[diffIndex])
            {
                int laneIndex = laneIndices[noteValue];
                if (laneIndex < NUM_LANES && statuses[diffIndex, laneIndex])
                {
                    Validate(diffIndex);
                    difficultyTracker[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        private const int SYSEX_DIFFICULTY_INDEX = 4;
        private const int SYSEX_TYPE_INDEX = 5;
        private const int SYSEX_STATUS_INDEX = 6;
        private const int OPEN_NOTE_TYPE = 1;
        private const byte SYSEX_ALL_DIFFICULTIES = 0xFF;
        private const int GREEN_INDEX = 1;

        protected override void ParseSysEx(ReadOnlySpan<byte> str)
        {
            if (str.StartsWith(SYSEXTAG) && str[SYSEX_TYPE_INDEX] == OPEN_NOTE_TYPE)
            {
                int status = str[SYSEX_STATUS_INDEX] == 0 ? 1 : 0;
                if (str[SYSEX_DIFFICULTY_INDEX] == SYSEX_ALL_DIFFICULTIES)
                {
                    for (int diff = 0; diff < NUM_DIFFICULTIES; ++diff)
                        laneIndices[NOTES_PER_DIFFICULTY * diff + GREEN_INDEX] = status;
                }
                else
                    laneIndices[NOTES_PER_DIFFICULTY * str[SYSEX_DIFFICULTY_INDEX] + GREEN_INDEX] = status;
            }
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.SequenceEqual(ENHANCED_STRINGS[0]) || str.SequenceEqual(ENHANCED_STRINGS[1]))
                for (int diff = 0; diff < NUM_DIFFICULTIES; ++diff)
                    laneIndices[NOTES_PER_DIFFICULTY * diff] = 0;
        }
    }
}
