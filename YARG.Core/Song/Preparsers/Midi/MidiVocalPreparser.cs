using System;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_Vocal_Preparser
    {
        private const int VOACAL_MIN = 36;
        private const int VOCAL_MAX = 84;
        private const int PERCUSSION_NOTE = 96;

        public static bool Parse(YARGMidiTrack track, bool isLeadVocals)
        {
            long vocalNote = -1;
            long lyric = -1;
            bool percussion = false;

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
                        {
                            vocalNote = track.Position;
                        }
                        else if (note.value == PERCUSSION_NOTE)
                        {
                            // percussion is invalid outside of PART VOCALS and HARM_1
                            percussion = isLeadVocals;
                        }
                    }
                    // NoteOff from this point
                    else if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
                    {
                        // Guarantees that a lyric-pitch pair is valid
                        if (vocalNote >= 0 && lyric >= vocalNote)
                        {
                            return true;
                        }
                        vocalNote = -1;
                    }
                    else if (note.value == PERCUSSION_NOTE && percussion)
                    {
                        return true;
                    }
                }
                else if (MidiEventType.Text <= track.Type && track.Type <= MidiEventType.Text_EnumLimit)
                {
                    var str = track.ExtractTextOrSysEx();
                    if (str.Length == 0 || str[0] != '[')
                    {
                        lyric = track.Position;
                    }
                }
            }
            return false;
        }
    }
}
