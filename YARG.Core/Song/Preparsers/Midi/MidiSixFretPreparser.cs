using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_SixFret_Preparser
    {
        private const int SIXFRET_MIN = 58;
        private const int SIXFRET_MAX = 103;
        // Open note included
        private const int NUM_LANES = 7;

        // Six fret indexing is fucked
        private static readonly int[] INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * MidiPreparser_Constants.NOTES_PER_DIFFICULTY]
        {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        public static unsafe DifficultyMask Parse(YARGMidiTrack track)
        {
            var validations = default(DifficultyMask);
            var difficulties = stackalloc bool[MidiPreparser_Constants.NUM_DIFFICULTIES];
            var statuses = stackalloc bool[MidiPreparser_Constants.NUM_DIFFICULTIES * NUM_LANES];

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.value < SIXFRET_MIN || note.value > SIXFRET_MAX)
                    {
                        continue;
                    }

                    int noteOffset = note.value - SIXFRET_MIN;
                    int diffIndex = MidiPreparser_Constants.DIFF_INDICES[noteOffset];
                    int laneIndex = INDICES[noteOffset];
                    if (difficulties[diffIndex] || laneIndex >= NUM_LANES)
                    {
                        continue;
                    }

                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        statuses[diffIndex * NUM_LANES + laneIndex] = true;
                    }
                    // Note off here
                    else if (statuses[diffIndex * NUM_LANES + laneIndex])
                    {
                        validations |= (DifficultyMask) (1 << (diffIndex + 1));
                        difficulties[diffIndex] = true;
                        if (validations == MidiPreparser_Constants.ALL_DIFFICULTIES)
                        {
                            break;
                        }
                    }
                }
            }
            return validations;
        }
    }
}
