using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_ProKeys_Preparser
    {
        private const int PROKEYS_MIN = 48;
        private const int PROKEYS_MAX = 72;
        private const int NOTES_IN_DIFFICULTY = PROKEYS_MAX - PROKEYS_MIN + 1;

        public static unsafe bool Parse(YARGMidiTrack track)
        {
            var statuses = stackalloc bool[NOTES_IN_DIFFICULTY];
            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (PROKEYS_MIN <= note.value && note.value <= PROKEYS_MAX)
                    {
                        if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                        {
                            statuses[note.value - PROKEYS_MIN] = true;
                        }
                        else if (statuses[note.value - PROKEYS_MIN])
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
