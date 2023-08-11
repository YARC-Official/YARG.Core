using System;

namespace YARG.Core.Song
{
    public class Midi_Vocal : MidiPreparser
    {
        private const int VOACAL_MIN = 36;
        private const int VOCAL_MAX = 84;
        private const int PERCUSSION_NOTE = 96;

        protected bool vocal = false;
        protected bool lyric = false;
        protected bool percussion = false;

        protected override bool ParseNote()
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
            {
                if (vocal && lyric)
                    return true;

                vocal = true;
                return false;
            }
            else if (note.value == PERCUSSION_NOTE)
                percussion = true;
            return false;
        }

        protected override bool ParseNote_Off()
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
                return vocal && lyric;
            else if (note.value == PERCUSSION_NOTE)
                return percussion;
            return false;
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.Length != 0 && str[0] != '[')
                lyric = true;
        }
    }

    public class Midi_Harmony : MidiPreparser
    {
        private const int VOACAL_MIN = 36;
        private const int VOCAL_MAX = 84;

        protected bool vocal = false;
        protected bool lyric = false;

        protected override bool ParseNote()
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
            {
                if (vocal && lyric)
                    return true;

                vocal = true;
                return false;
            }
            return false;
        }

        protected override bool ParseNote_Off()
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
                return vocal && lyric;
            return false;
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.Length != 0 && str[0] != '[')
                lyric = true;
        }
    }
}
