﻿using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal static class Midi_EliteDrums_Preparser
    {
        private const int ELITE_NOTES_PER_DIFFICULTY = 24;
        private const int NUM_LANES = 11;
        private const int ELITE_MAX = 82;

        public static unsafe DifficultyMask Parse(YARGMidiTrack track)
        {
            var validations = default(DifficultyMask);
            long statusBitMask = 0;

            var note = default(MidiNote);
            var stats = default(MidiStats);
            while (track.ParseEvent(ref stats))
            {
                if (stats.Type is MidiEventType.Note_On or MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    // Minimum is 0, so no minimum check required
                    if (note.Value > ELITE_MAX)
                    {
                        continue;
                    }

                    int diffIndex = MidiPreparser_Constants.EXTENDED_DIFF_INDICES[note.Value];
                    int laneIndex = MidiPreparser_Constants.EXTENDED_LANE_INDICES[note.Value];
                    var diffMask = (DifficultyMask)(1 << (diffIndex + 1));
                    if ((validations & diffMask) > 0 || laneIndex >= NUM_LANES)
                    {
                        continue;
                    }

                    long statusMask = 1L << (diffIndex * NUM_LANES + laneIndex);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                    {
                        statusBitMask |= statusMask;
                    }
                    // Note off here
                    else if ((statusBitMask & statusMask) > 0)
                    {
                        validations |= diffMask;
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
