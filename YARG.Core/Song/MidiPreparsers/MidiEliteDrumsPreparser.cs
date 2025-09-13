using System.Collections.Generic;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal static class Midi_EliteDrums_Preparser
    {
        private const int HAT_PEDAL_X = 72;
        private const int HAT_PEDAL_H = 48;
        private const int HAT_PEDAL_M = 24;
        private const int HAT_PEDAL_E = 0;

        private const int CYMBAL_CHANNEL_FLAG_Y = 11;
        private const int CYMBAL_CHANNEL_FLAG_B = 12;
        private const int CYMBAL_CHANNEL_FLAG_G = 13;


        private const int DOUBLE_KICK_NOTE = 73;
        private const int NUM_LANES = 11;
        private const int ELITE_MAX = 82;
        const int DOUBLE_KICK_MASK = 1 << (3 * NUM_LANES + 1);

        // Returns a separate DifficultyMask for the downchart because an Elite Drums chart that is entirely
        // unflagged stomps and/or splashes does not produce a playable downchart, so there might be fewer
        // downchart difficulties than Elite Drums difficulties.
        public static unsafe (DifficultyMask eliteDrumsDifficulties, DifficultyMask downchartDifficulties) Parse(YARGMidiTrack track)
        {
            var validations = default(DifficultyMask);
            var downchartValidations = default(DifficultyMask);

            long statusBitMask = 0;
            long downchartStatusBitMask = 0;

            var note = default(MidiNote);
            var stats = default(MidiStats);
            while (track.ParseEvent(ref stats))
            {
                if (stats.Type != MidiEventType.Note_On && stats.Type != MidiEventType.Note_Off)
                {
                    continue;
                }

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
                        downchartStatusBitMask |= DOUBLE_KICK_MASK;
                    }
                    // NoteOff here
                    else if ((statusBitMask & DOUBLE_KICK_MASK) > 0)
                    {
                        validations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
                        downchartValidations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
                    }
                }

                // Minimum is 0, so no minimum check required
                if (note.Value > ELITE_MAX)
                {
                    continue;
                }

                int diffIndex = MidiPreparser_Constants.EXTENDED_DIFF_INDICES[note.Value];
                int laneIndex = MidiPreparser_Constants.EXTENDED_LANE_INDICES[note.Value];
                var diffMask = (DifficultyMask) (1 << (diffIndex + 1));
                if ((validations & downchartValidations & diffMask) > 0 || laneIndex >= NUM_LANES)
                {
                    continue;
                }

                long statusMask = 1L << (diffIndex * NUM_LANES + laneIndex);
                long downchartStatusMask = 1L << (diffIndex * NUM_LANES + laneIndex);

                // Note Ons with no velocity equates to a note Off by spec
                if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                {
                    statusBitMask |= statusMask;

                    // Hat pedal notes don't contribute to a downchart difficulty, unless they're channel
                    // flagged to a cymbal
                    if ((note.Value is not (HAT_PEDAL_X or HAT_PEDAL_H or HAT_PEDAL_M or HAT_PEDAL_E)) ||
                        (stats.Channel is CYMBAL_CHANNEL_FLAG_Y or CYMBAL_CHANNEL_FLAG_B or CYMBAL_CHANNEL_FLAG_G ))
                    {
                        downchartStatusBitMask |= downchartStatusMask;
                    }
                }
                // Note off here
                else
                {
                    if ((statusBitMask & statusMask) > 0)
                    {
                        validations |= diffMask;
                    }

                    if ((downchartStatusBitMask & downchartStatusMask) > 0)
                    {
                        downchartValidations |= diffMask;
                    }

                    if ((validations & downchartValidations) == MidiPreparser_Constants.ALL_DIFFICULTIES_PLUS)
                    {
                        break;
                    }
                }

            }
            return (validations, downchartValidations);
        }
    }
}
