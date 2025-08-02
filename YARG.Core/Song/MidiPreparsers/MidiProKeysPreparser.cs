using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal static class Midi_ProKeys_Preparser
    {
        private const int PROKEYS_MIN = 48;
        private const int PROKEYS_MAX = 72;
        private const int NOTES_IN_DIFFICULTY = PROKEYS_MAX - PROKEYS_MIN + 1;

        public static unsafe bool Parse(YARGMidiTrack track)
        {
            int statusBitMask = 0;
            var note = default(MidiNote);
            var stats = default(MidiStats);
            while (track.ParseEvent(ref stats))
            {
                if (stats.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (PROKEYS_MIN <= note.Value && note.Value <= PROKEYS_MAX)
                    {
                        int statusMask = 1 << (note.Value - PROKEYS_MIN);
                        if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                        {
                            statusBitMask |= statusMask;
                        }
                        else if ((statusBitMask & statusMask) > 0)
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
