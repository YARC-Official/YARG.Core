using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_ProGuitar_Preparser
    {
        private const int NOTES_PER_DIFFICULTY = 24;
        private const int PROGUITAR_MAX = PROGUITAR_MIN + MidiPreparser_Constants.NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY;
        private const int PROGUITAR_MIN = 24;
        private const int NUM_STRINGS = 6;
        private const int MIN_VELOCITY = 100;
        private const int ARPEGGIO_CHANNEL = 1;
        private static readonly int[] PROGUITAR_DIFF_INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        private static readonly int[] PROGUITAR_LANE_INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * NOTES_PER_DIFFICULTY]{
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        };

        private const int MAXVELOCTIY_17 = 117;
        public static DifficultyMask Parse_17Fret(YARGMidiTrack track)
        {
            return Parse(track, MAXVELOCTIY_17);
        }

        private const int MAXVELOCITY_22 = 122;
        public static DifficultyMask Parse_22Fret(YARGMidiTrack track)
        {
            return Parse(track, MAXVELOCITY_22);
        }

        private static unsafe DifficultyMask Parse(YARGMidiTrack track, int maxVelocity)
        {
            var validations = default(DifficultyMask);
            var difficulties = stackalloc bool[MidiPreparser_Constants.NUM_DIFFICULTIES];
            var statuses = stackalloc bool[MidiPreparser_Constants.NUM_DIFFICULTIES * NUM_STRINGS];

            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.value < PROGUITAR_MIN || note.value > PROGUITAR_MAX)
                    {
                        continue;
                    }

                    int noteOffset = note.value - PROGUITAR_MIN;
                    int diffIndex = PROGUITAR_DIFF_INDICES[noteOffset];
                    int laneIndex = PROGUITAR_LANE_INDICES[noteOffset];
                    //                                                         Ghost notes aren't played
                    if (difficulties[diffIndex] || laneIndex >= NUM_STRINGS || track.Channel == ARPEGGIO_CHANNEL)
                    {
                        continue;
                    }

                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        if (MIN_VELOCITY <= note.velocity && note.velocity <= maxVelocity)
                        {
                            statuses[diffIndex * NUM_STRINGS + laneIndex] = true;
                        }
                    }
                    // Note off here
                    else if (statuses[diffIndex * NUM_STRINGS + laneIndex])
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
