using System;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public class Midi_Vocal_Preparser : Midi_Preparser
    {
        private const int VOCAL_PHRASE_1 = 105;
        private const int VOCAL_PHRASE_2 = 106;

        private const int VOCAL_MIN = 36;
        private const int VOCAL_MAX = 84;
        private const int PERCUSSION_NOTE = 96;

        private bool phrase = false;
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
            switch (note.value)
            {
                case >= VOCAL_MIN and <= VOCAL_MAX:
                    vocal = true;
                    break;

                case PERCUSSION_NOTE:
                    percussion = checkForPercussion;
                    break;

                case VOCAL_PHRASE_1 or VOCAL_PHRASE_2:
                    phrase = true;
                    break;

                default:
                    return false;
            }

            return IsFullyScanned();
        }

        protected override bool ParseNote_Off(YARGMidiTrack track)
        {
            return note.value switch
            {
                (>= VOCAL_MIN and <= VOCAL_MAX) or
                    PERCUSSION_NOTE or
                    VOCAL_PHRASE_1 or
                    VOCAL_PHRASE_2 => IsFullyScanned(),
                _ => false
            };
        }

        protected override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.Length == 0 || str[0] != '[')
                lyric = true;
        }

        private bool IsFullyScanned() =>
            // There must be at least one phrase marker
            phrase && (
                // In addition, there must be a vocal note and corresponding lyric...
                (vocal && lyric) ||
                // OR a percussion note
                (checkForPercussion && percussion)
            );
    }
}
