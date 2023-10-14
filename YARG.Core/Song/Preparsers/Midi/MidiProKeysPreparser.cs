using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_ProKeys_Preparser : Midi_Instrument_Preparser
    {
        private const int NOTES_PER_DIFFICULTY = 25;
        private const int PROKEYS_MIN = 48;
        private const int PROKEYS_MAX = 72;

        private readonly bool[] statuses = new bool[NOTES_PER_DIFFICULTY];

        private Midi_ProKeys_Preparser() { }

        public static bool Parse(YARGMidiTrack track)
        {
            Midi_ProKeys_Preparser preparser = new();
            return preparser.Process(track);
        }

        protected override bool IsNote() { return PROKEYS_MIN <= note.value && note.value <= PROKEYS_MAX; }

        protected override bool ParseLaneColor_ON(YARGMidiTrack track)
        {
            statuses[note.value - PROKEYS_MIN] = true;
            return false;
        }

        protected override bool ParseLaneColor_Off(YARGMidiTrack track)
        {
            return statuses[note.value - PROKEYS_MIN];
        }
    }
}
