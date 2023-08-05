using System;

namespace YARG.Core.Song
{
    public class Midi_Vocal : MidiPreparser
    {
        protected bool vocal = false;
        protected bool lyric = false;
        protected bool percussion = false;

        protected override bool ParseNote()
        {
            if (36 <= note.value && note.value <= 84)
            {
                if (vocal && lyric)
                    return true;

                vocal = true;
                return false;
            }
            else if (note.value == 96 || note.value == 97)
                percussion = true;
            return false;
        }

        protected override bool ParseNote_Off()
        {
            if (36 <= note.value && note.value <= 84)
                return vocal && lyric;
            else if (note.value == 96)
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
        protected bool vocal = false;
        protected bool lyric = false;

        protected override bool ParseNote()
        {
            if (36 <= note.value && note.value <= 84)
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
            if (36 <= note.value && note.value <= 84)
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
