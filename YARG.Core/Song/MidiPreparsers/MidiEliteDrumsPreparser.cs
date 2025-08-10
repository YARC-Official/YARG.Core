using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal static class Midi_EliteDrums_Preparser
    {
        private const int DOUBLE_KICK_NOTE = 73;
        private const int NUM_LANES = 11;
        private const int ELITE_MAX = 82;
        const int DOUBLE_KICK_MASK = 1 << (3 * NUM_LANES + 1);

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
                    if (note.Value == DOUBLE_KICK_NOTE)
                    {
                        if ((validations & DifficultyMask.ExpertPlus) > 0)
                        {
                            continue;
                        }

                        // Note Ons with no velocity equates to a note Off by spec
                        if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                        {
                            statusBitMask |= DOUBLE_KICK_MASK;
                        }
                        // NoteOff here
                        else if ((statusBitMask & DOUBLE_KICK_MASK) > 0)
                        {
                            validations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
                        }
                    }

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
                        if (validations == MidiPreparser_Constants.ALL_DIFFICULTIES_PLUS)
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
