using System;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_Vocal_Preparser : Midi_Preparser
    {
        private const int VOACAL_MIN = 36;
        private const int VOCAL_MAX = 84;
        private const int PERCUSSION_NOTE = 96;

        private bool vocal = false;
        private bool lyric = false;
        private bool percussion = false;
        private bool checkForPercussion;

        private Midi_Vocal_Preparser(bool checkForPercussion)
        {
            this.checkForPercussion = checkForPercussion;
        }

        public static bool ParseLeadTrack(YARGMidiTrack track)
        {
            Midi_Vocal_Preparser preparser = new(true);
            return preparser.Process(track);
        }

        public static bool ParseHarmonyTrack(YARGMidiTrack track)
        {
            Midi_Vocal_Preparser preparser = new(false);
            return preparser.Process(track);
        }

        protected override bool ParseNote_ON(YARGMidiTrack track)
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
            {
                if (vocal && lyric)
                    return true;
                vocal = true;
            }
            else if (checkForPercussion && note.value == PERCUSSION_NOTE)
                percussion = true;
            return false;
        }

        protected override bool ParseNote_Off(YARGMidiTrack track)
        {
            if (VOACAL_MIN <= note.value && note.value <= VOCAL_MAX)
                return vocal && lyric;
            return checkForPercussion && note.value == PERCUSSION_NOTE && percussion;
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.Length != 0 && str[0] != '[')
                lyric = true;
        }
    }
}
