using YARG.Core.IO;

namespace YARG.Core.Song
{
    public static class Midi_EliteDrums_Preparser
    {
        private const int ELITE_NOTES_PER_DIFFICULTY = 24;
        private const int NUM_LANES = 11;
        private const int ELITE_MAX = 82;

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
                    // Minimum is 0, so no minimum check required
                    if (note.value > ELITE_MAX)
                    {
                        continue;
                    }

                    int diffIndex = MidiPreparser_Constants.EXTENDED_DIFF_INDICES[note.value];
                    int laneIndex = MidiPreparser_Constants.EXTENDED_LANE_INDICES[note.value];
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
